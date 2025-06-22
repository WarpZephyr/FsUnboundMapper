using FsUnboundMapper.Binder;
using FsUnboundMapper.Cryptography;
using FsUnboundMapper.Exceptions;
using FsUnboundMapper.IO;
using FsUnboundMapper.Logging;
using libps3;
using SoulsFormats;
using System;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using XenonFormats;

namespace FsUnboundMapper
{
    internal class UnboundMapper
    {
        private string Root;
        private GameType Game;
        private PlatformType Platform;
        private RegionType Region;

        public UnboundMapper(string root)
        {
            Root = root;
            Game = AppConfig.Instance.ManualGameOverride;
            Platform = AppConfig.Instance.ManualPlatformOverride;
            Region = AppConfig.Instance.ManualRegionOverride;
        }

        public void Run()
        {
            Log.WriteLine("Running automatic detection where applicable...");
            DetectPlatform();
            DetectRoot();
            DetectGame();

            Log.WriteLine($"Determined platform as {Platform}.");
            Log.WriteLine($"Determined game as {Game}.");
            Log.WriteLine($"Determined region as {Region}.");
            Log.WriteLine($"Determined root folder as: \"{Root}\"");

            Log.WriteLine($"Mapping {Game}...");
            switch (Game)
            {
                case GameType.ArmoredCoreForAnswer:
                    RunAcfa();
                    break;
                case GameType.ArmoredCoreV:
                    RunAcv();
                    break;
                case GameType.ArmoredCoreVerdictDay:
                    RunAcvd();
                    break;
                default:
                    throw new UserErrorException("Could not determine a valid game.");
            }
        }

        private void RunAcfa()
        {
            // Lowercase all unpacked names
            string bindDir = Path.Combine(Root, "bind");
            if (!CheckDirectoryExists(bindDir))
                return;

            // Unpack "/bind/boot.bnd"
            //   - Unpack to "/" if not sorting
            //   - Sort files for later JP region versions
            UnpackBootAcfa(bindDir);

            // Unpack "/bind/boot_2nd.bnd"
            //   - Sort files
            //   - Unpack "/sfx_ps3_shader.bnd" to "/sfx/shader/" for the PS3 platform
            //   - Unpack "/sfx_xenon_shader.bnd" to "/sfx/shader/" for the Xbox 360 platform
            //   - Unpack "/dc0000_t.bnd" to "/model/decal/dc0000/" and move tpfs/xprs to "/tex/" below it
            //   - Unpack "/camouflage_t.bnd" to "/model/image/camouflage/" and move tpfs/xprs to "/tex/" below it
            //   - Unpack "/sandstorm_t.bnd" to "/model/radar/sandstorm/" and move tpfs/xprs to "/tex/" below it
            //   - Unpack "/dof_t.bnd" to "/model/filter/dof/" and move tpfs/xprs to "/tex/" below it
            //   - Unpack "/oldfilm_t.bnd" to "/model/filter/oldfilm/" and move tpfs/xprs to "/tex/" below it
            //   - Unpack "/lensflare_t.bnd" to "/model/filter/lensflare/" and move tpfs/xprs to "/tex/" below it
            //   - Unpack "/shakednoise_t.bnd" to "/model/filter/shakednoise/" and move tpfs/xprs to "/tex/" below it
            UnpackBoot2ndAcfa(bindDir);

            // Unpack "/bind/ingamescript.bnd" to "/"
            CheckUnpackBinder3(Path.Combine(bindDir, "ingamescript.bnd"), Root);

            // Unpack "/bind/menustay.bnd" to "/"
            CheckUnpackBinder3(Path.Combine(bindDir, "menustay.bnd"), Root);

            // Unpack "/bind/sortie.bnd" to "/"
            CheckUnpackBinder3(Path.Combine(bindDir, "sortie.bnd"), Root);

            // Unpack "/bind/tutorial.bnd" to "/"
            CheckUnpackBinder3(Path.Combine(bindDir, "tutorial.bnd"), Root);

            // Unpack "/bind/thumbnail.bnd" to "/thumbnail/"
            CheckUnpackBinder3(Path.Combine(bindDir, "thumbnail.bnd"),
                Path.Combine(Root, "thumbnail"));

            // Unpack "/bind/material.bnd" to "/material/"
            CheckUnpackBinder3(Path.Combine(bindDir, "material.bnd"),
                Path.Combine(Root, "material"));

            // Unpack "/bind/scene.bnd" to "/scene/"
            CheckUnpackBinder3(Path.Combine(bindDir, "scene.bnd"),
                Path.Combine(Root, "scene"));

            // Unpack "/bind/map_sound.bnd" to "/sound/"
            CheckUnpackBinder3(Path.Combine(bindDir, "map_sound.bnd"),
                Path.Combine(Root, "sound"));

            // Unpack "/bind/param/accolor.bnd" to "/param/accolor"
            CheckUnpackBinder3(Path.Combine(bindDir, "param", "accolor.bnd"),
                Path.Combine(Root, "param", "accolor"));

            // Unpack "/bind/model/image/arena_briefing.bnd" to "/model/image/arena_briefing/tex/"
            CheckUnpackTexBinder3(Path.Combine(bindDir, "model", "image", "arena_briefing.bnd"),
                Path.Combine(Root, "model", "image", "arena_briefing"));

            // Unpack "/bind/model/image/pc*.bnd" to "/model/image/pc*/tex/"
            UnpackEmblemPieceBindersAcfa(bindDir);

            // Unpack lang binders
            //   - Unpack each region: "es", "fr", "us", "jp"
            //   - Unpack "/bind/lang/{region}/briefing.bnd" to "/lang/{region}/briefing/"
            //   - Unpack "/bind/lang/{region}/emblem.bnd" to "/lang/{region}/emblem/"
            //   - Unpack "/bind/lang/{region}/menu.bnd" to "/lang/{region}/menu/"
            //   - Unpack "/bind/lang/{region}/nowload.bnd" to "/lang/{region}/nowload/tex/"
            UnpackLangBindersAcfa(bindDir);

            // Unpack all bnds under "/bind/event/" to "/"
            CheckUnpackBinder3s("missions", Path.Combine(bindDir, "event"),
                Root, "*.bnd", SearchOption.TopDirectoryOnly);

            // Unpack "/bind/sfx.bnd"
            //   - Unpack "/sfx_bin.bhd" to "/sfx/bin/sfx_bin.bhd"
            //   - Unpack "/sfx_bin.bdt" to "/sfx/bin/sfx_bin.bdt"
            //   - Unpack "/sfx_m.bnd" to "/model/sfx/"
            //   - Unpack "/sfx_t.bnd" to "/model/sfx/"
            //   - Unpack "/sphere.bin" to "/model/sfx/"
            UnpackSfxAcfa(bindDir);

            string bindModelDir = Path.Combine(bindDir, "model");
            if (CheckDirectoryExists(bindModelDir))
            {
                // Unpack "/bind/model/ene.bnd"
                //   - Unpack "/e{id}*" to "/model/ene/e{id}/e{id}*"
                UnpackModelsAcfa(Path.Combine(bindModelDir, "ene.bnd"),
                    Path.Combine(Root, "model", "ene"), 5);

                // Unpack "/bind/model/obj.bnd"
                //   - Unpack "/o{id}*" to "/model/obj/o{id}/o{id}*"
                UnpackModelsAcfa(Path.Combine(bindModelDir, "obj.bnd"),
                    Path.Combine(Root, "model", "obj"), 5);

                // Unpack "/bind/model/map.bnd"
                //   - Unpack "/m{id}*" to "/model/map/m{id}/m{id}*"
                UnpackModelsAcfa(Path.Combine(bindModelDir, "map.bnd"),
                    Path.Combine(Root, "model", "map"), 4);

                // Unpack "/bind/model/break.bnd"
                //   - Unpack "/b{id}*" to "/model/break/b{id}/b{id}*"
                UnpackModelsAcfa(Path.Combine(bindModelDir, "break.bnd"),
                    Path.Combine(Root, "model", "break"), 5);

                // Unpack AcParts models
                //   - Unpack "/bind/model/ac/motion/{animType}.bnd" to "/model/ac/motion/{animType}/"
                //   - Unpack each quality: "ac", "garage"
                //   - Unpack each kind: "parts", "sub"
                //   - Unpack each category.
                //   - Unpack "/bind/model/{quality}/{kind}_{categoryLong}.bnd" to "/model/{quality}/{kind}/{categoryLong}/"
                //     - Unpack "/{category}{id}*" to "/model/{quality}/{kind}/{categoryLong}/{category}{id}/{category}{id}*"
                BinderUnpacker.UnpackBinder3sAsFolders(Path.Combine(bindModelDir, "ac", "motion"),
                    Path.Combine(Root, "model", "ac", "motion"),
                    "*.bnd", SearchOption.TopDirectoryOnly);

                UnpackAcModelsAcfa(bindModelDir, "ac");
                UnpackAcModelsAcfa(bindModelDir, "garage");
            }

            if (AppConfig.Instance.HidePackedFiles &&
                Platform != PlatformType.Xbox360) // For now ACFA Xbox 360 loose loading doesn't appear to work
            {
                HideDirectory(Root, "bind");

                string modelAcPath = Path.Combine(Root, "model", "ac");
                HideFile(modelAcPath, "parts_arm.bnd");
                HideFile(modelAcPath, "parts_back.bnd");
                HideFiles(Path.Combine(modelAcPath, "motion"), "*.bnd");
            }
        }

        private void RunAcv()
        {
            string bindDir = Path.Combine(Root, "bind");
            if (!CheckDirectoryExists(bindDir))
                return;

            UnpackScripts(bindDir);
            Log.WriteLine("Unpacking \"dvdbnd5.bhd\"...");
            string headerPath = Path.Combine(bindDir, "dvdbnd5.bhd");
            string dataPath = Path.Combine(bindDir, "dvdbnd.bdt");
            if (!CheckSplitFileExists(headerPath, dataPath))
                return;

            BinderUnpacker.UnpackEbl(headerPath, dataPath, Root, Game, Platform);
            CheckUnpackBinder3(Path.Combine(bindDir, "boot.bnd"), Root);
            CheckUnpackBinder3(Path.Combine(bindDir, "boot_2nd.bnd"), Root);
            CheckUnpackBinder3s("missions", Path.Combine(bindDir, "mission"),
                Root, "*.bnd", SearchOption.TopDirectoryOnly);

            string modelMapDir = Path.Combine(Root, "model", "map");
            if (CheckDirectoryExists(modelMapDir))
                PackAcvMaps(modelMapDir);

            string soundDir = Path.Combine(Root, "sound");
            if (AppConfig.Instance.ApplyFmodCrashFix &&
                CheckDirectoryExists(soundDir))
                ApplyFmodCrashFix(soundDir, "se_weapon.fsb");

            if (AppConfig.Instance.HidePackedFiles)
                HideFile(bindDir, "dvdbnd5.bhd");
        }

        private void RunAcvd()
        {
            string bindDir = Path.Combine(Root, "bind");
            if (!CheckDirectoryExists(bindDir))
                return;

            UnpackScripts(bindDir);
            Log.WriteLine("Unpacking \"dvdbnd5_layer0.bhd\"...");
            string headerPath0 = Path.Combine(bindDir, "dvdbnd5_layer0.bhd");
            string dataPath0 = Path.Combine(bindDir, "dvdbnd_layer0.bdt");
            if (!CheckSplitFileExists(headerPath0, dataPath0))
                return;

            BinderUnpacker.UnpackEbl(headerPath0, dataPath0, Root, Game, Platform);
            if (Platform == PlatformType.Xbox360)
            {
                Log.WriteLine("Unpacking \"dvdbnd5_layer1.bhd\"...");
                string headerPath1 = Path.Combine(bindDir, "dvdbnd5_layer1.bhd");
                string dataPath1 = Path.Combine(bindDir, "dvdbnd_layer1.bdt");
                if (!CheckSplitFileExists(headerPath1, dataPath1))
                    return;

                BinderUnpacker.UnpackEbl(headerPath1, dataPath1, Root, Game, Platform);
            }

            CheckUnpackBinder3(Path.Combine(bindDir, "boot.bnd"), Root);
            CheckUnpackBinder3(Path.Combine(bindDir, "boot_2nd.bnd"), Root);
            CheckUnpackBinder3s("missions", Path.Combine(bindDir, "mission"),
                Root, "*.bnd", SearchOption.TopDirectoryOnly);

            string soundDir = Path.Combine(Root, "sound");
            if (AppConfig.Instance.ApplyFmodCrashFix &&
                CheckDirectoryExists(soundDir))
                ApplyFmodCrashFix(soundDir, "se_weapon.fsb");

            if (AppConfig.Instance.HidePackedFiles)
            {
                HideFile(bindDir, "dvdbnd5_layer0.bhd");
                if (Platform == PlatformType.Xbox360)
                {
                    HideFile(bindDir, "dvdbnd5_layer1.bhd");
                }
            }
        }

        #region Game Run Helpers

        private void UnpackBootAcfa(string bindDir)
        {
            Log.WriteLine("Unpacking boot...");
            string bootPath = Path.Combine(bindDir, "boot.bnd");
            if (!CheckFileExists(bootPath))
                return;

            using var bnd = new BND3Reader(bootPath);
            if ((bnd.Format & SoulsFormats.Binder.Format.Names2) != 0)
            {
                // The BND3 already has full pathing
                BinderUnpacker.UnpackBinder3(bootPath, Root);
                return;
            }

            // Should only really become jp for lang region in this case
            string langRegion;
            switch (Region)
            {
                case RegionType.UnitedStates:
                case RegionType.Europe:
                    langRegion = "us";
                    break;
                case RegionType.Japan:
                case RegionType.Asia:
                    langRegion = "jp";
                    break;
                case RegionType.Spanish:
                    langRegion = "es";
                    break;
                case RegionType.France:
                    langRegion = "fr";
                    break;
                default:
                    langRegion = string.Empty;
                    break;
            }

            foreach (var file in bnd.Files)
            {
                string outPath;
                string outName = PathCleaner.CleanComponentPath(file.Name);
                string fileName = Path.GetFileName(outName); // Just in case...
                switch (fileName)
                {
                    case "enemyparts.bin":
                    case "fcs.txt":
                    case "weapon.txt":
                        outName = Path.Combine("param", "enemy", "parts", fileName);
                        break;
                    case "bulletlaunchenemy.bin":
                    case "enemybulletblade.bin":
                    case "enemybulletecm.bin":
                    case "enemybulletenergy.bin":
                    case "enemybulletexplosion.bin":
                    case "enemybulletglue.bin":
                    case "enemybulletmissile.bin":
                    case "enemybulletrigid.bin":
                        outName = Path.Combine("param", "enemy", "bullet", fileName);
                        break;
                    case "enemybasicparam.bin":
                    case "enemycommon.bin":
                    case "enemygraphicsparam.bin":
                    case "enemyperformanceparam.bin":
                    case "enemyweapon.bin":
                        outName = Path.Combine("enemy", fileName);
                        break;
                    case "accel.bin":
                    case "paramlist.xml":
                        outName = Path.Combine("system", fileName);
                        break;
                    case "fontdef.xml":
                        outName = Path.Combine("font", fileName);
                        break;
                    case "font.xvu":
                    case "font.xpu":
                    case "font.vpo":
                    case "font.fpo":
                        outName = Path.Combine("shader", "font", fileName);
                        break;
                    case "ac45_allsound.xgs":
                    case "ac45_allsound.mgs":
                    case "magicorchestra.ini":
                    case "bankset.bin":
                        outName = Path.Combine("sound", fileName);
                        break;
                    case "keyguide.bin":
                    case "skeyguide.bin":
                        outName = Path.Combine("lang", langRegion, "system", fileName);
                        break;
                    case "assemmenu.bin":
                    case "acassemblydrawing.bin":
                    case "actextinfo.bin":
                    case "missioninfo.bin":
                    case "trial_missioninfo.bin":
                        outName = Path.Combine("lang", langRegion, "param", fileName);
                        break;
                    case "menusystem.drb":
                    case "menusystem.xpr":
                    case "menusystem.tpf":
                        outName = Path.Combine("lang", langRegion, "menu", fileName);
                        break;
                    case "partsexplain_en.fmg":
                        outName = Path.Combine("lang", "us", "text", fileName);
                        break;
                    case "partsexplain_jp.fmg":
                        outName = Path.Combine("lang", "jp", "text", fileName);
                        break;
                    case "partsexplain_es.fmg":
                        outName = Path.Combine("lang", "es", "text", fileName);
                        break;
                    case "partsexplain_fr.fmg":
                        outName = Path.Combine("lang", "fr", "text", fileName);
                        break;
                    case "dialog.fmg":
                    case "menu.fmg":
                    case "linehelp_ps3.fmg":
                    case "linehelp_xbox.fmg":
                        outName = Path.Combine("lang", langRegion, "text", "menu", fileName);
                        break;
                    case "e1_t.bnd":
                    case "e2_t.bnd":
                    case "e3_t.bnd":
                    case "e4_t.bnd":
                    case "e5_t.bnd":
                    case "e6_t.bnd":
                    case "e7_t.bnd":
                    case "e8_t.bnd":
                    case "e9_t.bnd":
                    case "e10_t.bnd":
                    case "e11_t.bnd":
                    case "e12_t.bnd":
                    case "j1_t.bnd":
                    case "j2_t.bnd":
                    case "s1_ps3_t.bnd":
                    case "s1_xbox_t.bnd":
                        outName = Path.Combine("font", Path.GetFileNameWithoutExtension(outName).Replace("_t", string.Empty), fileName);
                        break;
                    case "boot_shader.bnd":
                        // Handle this earlier to preserve existing shader BNDs with better file dates
                        outName = Path.Combine("shader", fileName);
                        outPath = PathCleaner.CreatePath(Root, outName);
                        if (File.Exists(outPath))
                            continue;

                        BinderUnpacker.UnpackBinderFile(bnd, file, outPath);
                        continue;
                    default:
                        // Examples:
                        // aaparam.def
                        // acactrestrictionparam.def
                        if (fileName.EndsWith(".def"))
                        {
                            outName = Path.Combine("param", "def", fileName);
                        }
                        // Examples:
                        // acanimparam.dbp
                        // acsisdisplaycommon.dbp
                        else if (fileName.EndsWith(".dbp"))
                        {
                            outName = Path.Combine("dbmenu", fileName);
                        }
                        // Examples:
                        // bgm.xib
                        // bgm.xwb
                        // bgm.xsb
                        // bgm.mib
                        // bgm.mwb
                        // bgm.msb
                        else if (fileName.EndsWith(".xib") ||
                            fileName.EndsWith(".xwb") ||
                            fileName.EndsWith(".xsb") ||
                            fileName.EndsWith(".mib") ||
                            fileName.EndsWith(".mwb") ||
                            fileName.EndsWith(".msb")) // We should have already handled any actual map MSBs before the sound MSBs (MOSB)
                        {
                            outName = Path.Combine("sound", fileName);
                        }
                        // Examples:
                        // e1.ccm
                        // e2.ccm
                        // e3.ccm
                        // j1.ccm
                        // j2.ccm
                        // e10.ccf
                        // s1_xbox.ccf
                        // s1_ps3.ccf
                        else if (fileName.EndsWith(".ccm") ||
                            fileName.EndsWith(".ccf"))
                        {
                            outName = Path.Combine("font", Path.GetFileNameWithoutExtension(outName), fileName);
                        }
                        // Examples:
                        // m020_GrassField.mtd
                        // e4220.mtd
                        // DefaultAlpha.mtd
                        else if (fileName.EndsWith(".mtd"))
                        {
                            outName = Path.Combine("material", "mtd", fileName);
                        }
                        // Examples:
                        // color0000.bin
                        // color8010.bin
                        else if (fileName.Length == 13 &&
                            fileName.StartsWith("color") &&
                            fileName.EndsWith(".bin"))
                        {
                            outName = Path.Combine("param", "accolor", fileName);
                        }
                        else if (fileName.EndsWith(".bin")
                            || fileName.EndsWith(".param")
                            || fileName.EndsWith(".xml"))
                        {
                            // A few param files have the correct extension:
                            // destroyap.param
                            // destroyfall.param
                            // flocking.param

                            // The xmls that go here:
                            // jcondata.xml
                            // enemyjcondata.xml

                            // We should have handled all non-param bins by now
                            // The outliers that go here:
                            // partsregulation.bin
                            // regulation.bin
                            // acparts.bin
                            // trial_acparts.bin
                            outName = Path.Combine("param", fileName);
                        }
                        else
                        {
                            // Just in case...
                            Log.WriteLine($"Warning: No known sort path for boot.bnd file: \"{file.Name}\"");
                            continue;
                        }
                        break;
                }

                outPath = PathCleaner.CreatePath(Root, outName);
                BinderUnpacker.UnpackBinderFile(bnd, file, outPath);
            }
        }

        private void UnpackBoot2ndAcfa(string bindDir)
        {
            Log.WriteLine("Unpacking boot_2nd...");
            string bootPath = Path.Combine(bindDir, "boot_2nd.bnd");
            if (!CheckFileExists(bootPath))
                return;

            using var bnd = new BND3Reader(bootPath);
            foreach (var file in bnd.Files)
            {
                string outName = PathCleaner.CleanComponentPath(file.Name);
                string outPath;
                string fileName = Path.GetFileName(outName); // Just in case...
                switch (fileName)
                {
                    case "sfx_xenon_shader.bnd":
                    case "sfx_ps3_shader.bnd":
                        // Needs to be unpacked for some reason
                        outName = Path.Combine("sfx", "shader");
                        outPath = PathCleaner.CreatePath(Root, outName);
                        BinderUnpacker.UnpackBinder3(bnd.ReadFile(file), outPath);
                        continue;
                    case "debug_shader.bnd":
                    case "filter_shader.bnd":
                    case "flver_shader.bnd":
                    case "static_shader.bnd":
                        // Handle this earlier to preserve existing shader BNDs with better file dates
                        outName = Path.Combine("shader", fileName);
                        outPath = PathCleaner.CreatePath(Root, outName);
                        if (File.Exists(outPath))
                            continue;

                        BinderUnpacker.UnpackBinderFile(bnd, file, outPath);
                        continue;
                    case "dc0000_t.bnd":
                        // Needs to be unpacked for some reason
                        outName = Path.Combine("model", "decal", "dc0000");
                        outPath = Path.Combine(Root, outName);
                        UnpackTexBinder3(bnd.ReadFile(file), outPath);
                        continue;
                    case "camouflage_t.bnd":
                        // Needs to be unpacked for some reason
                        outName = Path.Combine("model", "image", "camouflage");
                        outPath = Path.Combine(Root, outName);
                        UnpackTexBinder3(bnd.ReadFile(file), outPath);
                        continue;
                    case "sandstorm_t.bnd":
                        // Needs to be unpacked for some reason
                        outName = Path.Combine("model", "radar", "sandstorm");
                        outPath = Path.Combine(Root, outName);
                        UnpackTexBinder3(bnd.ReadFile(file), outPath);
                        continue;
                    case "dof_t.bnd":
                    case "oldfilm_t.bnd":
                    case "lensflare_t.bnd":
                    case "shakednoise_t.bnd":
                        // Needs to be unpacked for some reason
                        outName = Path.Combine("model", "filter", Path.GetFileNameWithoutExtension(outName).Replace("_t", string.Empty));
                        outPath = Path.Combine(Root, outName);
                        UnpackTexBinder3(bnd.ReadFile(file), outPath);
                        continue;
                    case "system_env.msb":
                        outName = Path.Combine("model", "system", fileName);
                        break;
                    case "assemmenu.bin":
                        // Ignore as other archives have better pathing for its many region copies
                        continue;
                    default:
                        if (outName.EndsWith(".mtd"))
                        {
                            outName = Path.Combine("material", "mtd", fileName);
                        }
                        else if (outName.EndsWith(".mib") ||
                            outName.EndsWith(".mwb") ||
                            outName.EndsWith(".msb") ||
                            outName.EndsWith(".xib") ||
                            outName.EndsWith(".xwb") ||
                            outName.EndsWith(".xsb"))
                        {
                            outName = Path.Combine("sound", fileName);
                        }
                        else
                        {
                            // Just in case...
                            Log.WriteLine($"Warning: No known sort path for boot.bnd file: \"{file.Name}\"");
                            continue;
                        }
                        break;
                }

                outPath = PathCleaner.CreatePath(Root, outName);
                BinderUnpacker.UnpackBinderFile(bnd, file, outPath);
            }
        }

        private void UnpackEmblemPieceBindersAcfa(string bindDir)
        {
            // Handle the expected 8 pcXXXX.bnd files
            Log.WriteLine("Unpacking emblem pieces...");
            int pcTexIndex = 1;
            string pcTexName = $"pc{pcTexIndex:D4}";
            string pcTexPath = Path.Combine(bindDir, "model", "image", $"{pcTexName}.bnd");
            if (CheckFileExists(pcTexPath))
                UnpackTexBinder3(pcTexPath, Path.Combine(Root, "model", "image", pcTexName));

            void NextPcTexPath()
            {
                pcTexIndex++;
                pcTexName = $"pc{pcTexIndex:D4}";
                pcTexPath = Path.Combine(bindDir, "model", "image", $"{pcTexName}.bnd");
            }

            NextPcTexPath();
            if (CheckFileExists(pcTexPath))
                UnpackTexBinder3(pcTexPath, Path.Combine(Root, "model", "image", pcTexName));

            NextPcTexPath();
            if (CheckFileExists(pcTexPath))
                UnpackTexBinder3(pcTexPath, Path.Combine(Root, "model", "image", pcTexName));

            NextPcTexPath();
            if (CheckFileExists(pcTexPath))
                UnpackTexBinder3(pcTexPath, Path.Combine(Root, "model", "image", pcTexName));

            NextPcTexPath();
            if (CheckFileExists(pcTexPath))
                UnpackTexBinder3(pcTexPath, Path.Combine(Root, "model", "image", pcTexName));

            NextPcTexPath();
            if (CheckFileExists(pcTexPath))
                UnpackTexBinder3(pcTexPath, Path.Combine(Root, "model", "image", pcTexName));

            NextPcTexPath();
            if (CheckFileExists(pcTexPath))
                UnpackTexBinder3(pcTexPath, Path.Combine(Root, "model", "image", pcTexName));

            NextPcTexPath();
            if (CheckFileExists(pcTexPath))
                UnpackTexBinder3(pcTexPath, Path.Combine(Root, "model", "image", pcTexName));

            // Handle any extra (potentially modded...) pcXXXX.bnd files
            NextPcTexPath();
            while (File.Exists(pcTexPath))
            {
                UnpackTexBinder3(pcTexPath, Path.Combine(Root, "model", "image", pcTexName));
                NextPcTexPath();
            }
        }

        private void UnpackLangBindersAcfa(string bindDir)
        {
            string bindLangDir = Path.Combine(bindDir, "lang");
            if (CheckDirectoryExists(bindLangDir))
            {
                string bindLangJpDir = Path.Combine(bindLangDir, "jp");
                if (CheckDirectoryExists(bindLangJpDir))
                {
                    // Should always exist
                    Log.WriteLine("Unpacking jp lang...");
                    CheckSilentUnpackBinder3(
                        Path.Combine(bindLangJpDir, "emblem.bnd"),
                        Path.Combine(Root, "lang", "jp", "emblem"));

                    CheckSilentUnpackTexBinder3(
                        Path.Combine(bindLangJpDir, "nowload.bnd"),
                        Path.Combine(Root, "lang", "jp", "nowload"));

                    CheckSilentUnpackBinder3(
                        Path.Combine(bindLangJpDir, "briefing.bnd"),
                        Path.Combine(Root, "lang", "jp", "briefing"));

                    CheckSilentUnpackBinder3(
                        Path.Combine(bindLangJpDir, "menu.bnd"),
                        Path.Combine(Root, "lang", "jp", "menu"));
                }

                Log.WriteLine("Unpacking other langs...");
                foreach (string folder in Directory.EnumerateDirectories(bindLangDir))
                {
                    string name = Path.GetFileName(folder);
                    if (name == "jp")
                        continue; // Already handled

                    string bindLangRegionDir = Path.Combine(bindLangDir, name);
                    CheckSilentUnpackBinder3(
                        Path.Combine(bindLangRegionDir, "briefing.bnd"),
                        Path.Combine(Root, "lang", name, "briefing"));

                    CheckSilentUnpackBinder3(
                        Path.Combine(bindLangRegionDir, "menu.bnd"),
                        Path.Combine(Root, "lang", name, "menu"));
                }
            }
        }

        private void UnpackSfxAcfa(string bindDir)
        {
            Log.WriteLine("Unpacking sfx...");
            string sfxBndPath = Path.Combine(bindDir, "sfx.bnd");
            if (!CheckFileExists(sfxBndPath))
                return;

            using var bnd = new BND3Reader(sfxBndPath);
            foreach (var file in bnd.Files)
            {
                string outPath;
                string fileName = Path.GetFileName(file.Name).ToLowerInvariant(); // Just in case...
                switch (fileName)
                {
                    case "sfx_bin.bhd":
                    case "sfx_bin.bdt":
                        outPath = PathCleaner.CreatePath(Path.Combine(Root, "sfx", "bin"), fileName);
                        BinderUnpacker.UnpackBinderFile(bnd, file, outPath);
                        break;
                    case "sfx_m.bnd":
                    case "sfx_t.bnd":
                    case "sphere.bin":
                        outPath = PathCleaner.CreatePath(Path.Combine(Root, "model", "sfx"), fileName);
                        BinderUnpacker.UnpackBinderFile(bnd, file, outPath);
                        break;
                }
            }
        }

        private static void UnpackModelsBinderAcfa(BND3Reader bnd, string outDir, int idLength)
        {
            foreach (var file in bnd.Files)
            {
                string fileName = Path.GetFileName(file.Name).ToLowerInvariant(); // Just in case...
                string dirName = fileName[..idLength];
                string bOutDir = Path.Combine(outDir, dirName);
                string outPath = PathCleaner.CreatePath(bOutDir, fileName);
                BinderUnpacker.UnpackBinderFile(bnd, file, outPath);
            }
        }

        private static void UnpackModelsAcfa(string path, string outDir, int idLength)
        {
            Log.WriteLine($"Unpacking {Path.GetFileNameWithoutExtension(path)} models...");
            if (!CheckFileExists(path))
                return;

            using var bnd = new BND3Reader(path);
            UnpackModelsBinderAcfa(bnd, outDir, idLength);
        }

        private void UnpackAcModelsAcfa(string baseDir, string dirName)
        {
            Log.WriteLine($"Unpacking {dirName} models...");
            string dir = Path.Combine(baseDir, dirName);
            if (!CheckDirectoryExists(dir))
                return;

            foreach (string file in Directory.EnumerateFiles(dir, "*.bnd", SearchOption.TopDirectoryOnly))
            {
                string kind;
                string categoryLong;
                string fileName = Path.GetFileNameWithoutExtension(file);
                int underScoreIndex = fileName.IndexOf('_');
                if (underScoreIndex != -1)
                {
                    kind = fileName[..underScoreIndex];
                    categoryLong = fileName[(underScoreIndex + 1)..];
                }
                else
                {
                    Log.WriteLine($"Warning: No known sort path for {dirName} model file: \"{file}\"");
                    continue;
                }

                string outDir = Path.Combine(Root, "model", dirName, kind, categoryLong);
                using var bnd = new BND3Reader(file);
                UnpackModelsBinderAcfa(bnd, outDir, 6);
            }
        }

        private void UnpackScripts(string bindDir)
        {
            Log.WriteLine("Unpacking script binders...");
            string scriptHeaderPath = Path.Combine(bindDir, "script.bhd");
            string scriptDataPath = Path.Combine(bindDir, "script.bdt");
            if (Platform == PlatformType.PlayStation3)
            {
                SdatDecryptor.DecryptIfExists(scriptHeaderPath);
                SdatDecryptor.DecryptIfExists(scriptDataPath);
            }

            if (!CheckSplitFileExists(scriptHeaderPath, scriptDataPath))
                return;

            string aiScriptDir = Path.Combine(Root, "airesource", "script");
            string sceneScriptDir = Path.Combine(Root, "scene");

            using var bnd = new BXF3Reader(scriptHeaderPath, scriptDataPath);
            foreach (var file in bnd.Files)
            {
                string outName = PathCleaner.CleanComponentPath(file.Name);
                string baseDir = outName.EndsWith("scene.lc", StringComparison.InvariantCultureIgnoreCase) ? sceneScriptDir : aiScriptDir;
                string outPath = PathCleaner.CreatePath(baseDir, outName);
                BinderUnpacker.UnpackBinderFile(bnd, file, outPath);
            }
        }

        private static void PackAcvMaps(string dir)
        {
            Log.WriteLine("Packing Armored Core V map models and textures...");
            foreach (var directory in Directory.EnumerateDirectories(dir, "m*", SearchOption.TopDirectoryOnly))
            {
                PackAcvMap(directory);
            }
        }

        private static void PackAcvMap(string dir)
        {
            static void SetBinderInfo(BND3 bnd)
            {
                bnd.Version = "JP100";
                bnd.Format = SoulsFormats.Binder.Format.IDs | SoulsFormats.Binder.Format.Names1 | SoulsFormats.Binder.Format.Compression;
                bnd.BitBigEndian = true;
                bnd.BigEndian = true;
                bnd.Unk18 = 0;
                for (int i = 0; i < bnd.Files.Count; i++)
                {
                    bnd.Files[i].Flags = SoulsFormats.Binder.FileFlags.Flag1;
                    bnd.Files[i].ID = i;
                }
            }

            string mapID = Path.GetFileName(dir);
            string modelBNDPath = Path.Combine(dir, $"{mapID}_m.dcx.bnd");
            string textureBNDPath = Path.Combine(dir, $"{mapID}_htdcx.bnd");

            var modelBND = BinderUnpacker.PackBinder3(dir, [".flv", ".hmd", ".smd", ".mlb"]);
            var textureBND = BinderUnpacker.PackBinder3(dir, ".tpf.dcx", "_l.tpf.dcx");
            SetBinderInfo(modelBND);
            SetBinderInfo(textureBND);
            modelBND.Write(modelBNDPath);
            textureBND.Write(textureBNDPath);
        }

        private static void ApplyFmodCrashFix(string soundDir, string fmodName)
        {
            string seWeaponPath = Path.Combine(soundDir, fmodName);
            var soundFI = new FileInfo(seWeaponPath);
            int expandLength = 20_000_000;
            if (soundFI.Exists && soundFI.Length < expandLength)
            {
                Log.WriteLine($"Expanding {fmodName} to fix fmod crash...");
                Expand(seWeaponPath, expandLength);
            }
        }

        #endregion

        #region Binder

        private static void UnpackTexBinder3(string path, string destDir)
        {
            using BinderReader bnd = new BND3Reader(path);
            UnpackTexBinder(bnd, destDir);
        }

        private static void UnpackTexBinder3(byte[] bytes, string destDir)
        {
            using BinderReader bnd = new BND3Reader(bytes);
            UnpackTexBinder(bnd, destDir);
        }

        private static void UnpackTexBinder(BinderReader bnd, string destDir)
        {
            foreach (var file in bnd.Files)
            {
                string outName = PathCleaner.CleanComponentPath(file.Name);
                if (outName.EndsWith(".tpf") ||
                    outName.EndsWith(".xpr"))
                {
                    outName = Path.Combine("tex", outName);
                }

                string outPath = PathCleaner.CreatePath(destDir, outName);
                BinderUnpacker.UnpackBinderFile(bnd, file, outPath);
            }
        }

        private static void CheckUnpackBinder3(string path, string outPath)
        {
            Log.WriteLine($"Unpacking {Path.GetFileNameWithoutExtension(path)}...");
            if (CheckFileExists(path))
                BinderUnpacker.UnpackBinder3(path, outPath);
        }

        private static void CheckSilentUnpackBinder3(string path, string outPath)
        {
            if (CheckFileExists(path))
                BinderUnpacker.UnpackBinder3(path, outPath);
        }

        private static void CheckUnpackTexBinder3(string path, string outPath)
        {
            Log.WriteLine($"Unpacking {Path.GetFileNameWithoutExtension(path)}...");
            if (CheckFileExists(path))
                UnpackTexBinder3(path, outPath);
        }

        private static void CheckSilentUnpackTexBinder3(string path, string outPath)
        {
            if (CheckFileExists(path))
                UnpackTexBinder3(path, outPath);
        }

        private static void CheckUnpackBinder3s(string name, string dir, string outDir, string wildcard, SearchOption searchOption)
        {
            Log.WriteLine($"Unpacking {name}...");
            if (CheckDirectoryExists(dir))
                BinderUnpacker.UnpackBinder3s(dir, outDir, wildcard, searchOption);
        }

        #endregion

        #region File

        private static void HideFile(string dir, string name)
        {
            string path = Path.Combine(dir, name);
            if (!File.Exists(path))
            {
                return;
            }

            if (!name.StartsWith('-'))
            {
                Log.WriteLine($"Renaming \"{name}\" to ensure the game does not find it...");

                string newPath = Path.Combine(dir, $"-{name}");
                File.Move(path, newPath);
                Log.WriteLine($"Renamed \"{name}\" to \"-{name}\"");
            }
        }

        private static void HideFiles(string dir, string wildcard)
        {
            foreach (string file in Directory.EnumerateFiles(dir, wildcard, SearchOption.TopDirectoryOnly))
            {
                string name = Path.GetFileName(file);
                if (!name.StartsWith('-'))
                {
                    Log.WriteLine($"Renaming \"{name}\" to ensure the game does not find it...");

                    string newPath = Path.Combine(dir, $"-{name}");
                    File.Move(file, newPath);
                    Log.WriteLine($"Renamed \"{name}\" to \"-{name}\"");
                }
            }
        }

        private static void HideDirectory(string dir, string name)
        {
            string path = Path.Combine(dir, name);
            if (!Directory.Exists(path))
                return;

            if (!name.StartsWith('-'))
            {
                Log.WriteLine($"Renaming \"{name}\" to ensure the game does not find it...");

                string newPath = Path.Combine(dir, $"-{name}");
                Directory.Move(path, newPath);
                Log.WriteLine($"Renamed \"{name}\" to \"-{name}\"");
            }
        }

        private static void Expand(string path, int length, int chunkSize = 65536)
        {
            using FileStream fs = new FileStream(path, FileMode.Append, FileAccess.Write, FileShare.Read, chunkSize, FileOptions.SequentialScan);
            int totalChunks = length / chunkSize;
            for (int i = 0; i < totalChunks; i++)
            {
                fs.Write(new byte[chunkSize], 0, chunkSize);
                length -= chunkSize;
            }

            fs.Write(new byte[length], 0, length);
        }

        #endregion

        #region Path

        private static bool CheckDirectoryExists(string path)
        {
            if (!Directory.Exists(path))
            {
                Log.WriteLine($"Warning: Could not find \"{Path.GetFileName(path)}\" folder, game was not unpacked correctly or is missing files.");
                return false;
            }

            return true;
        }

        private static bool CheckFileExists(string path)
        {
            if (!File.Exists(path))
            {
                Log.WriteLine($"Warning: Could not find \"{Path.GetFileName(path)}\" file, game was not unpacked correctly or is missing files.");
                return false;
            }

            return true;
        }

        private static bool CheckSplitFileExists(string headerPath, string dataPath)
            => CheckFileExists(headerPath) && CheckFileExists(dataPath);

        #endregion

        #region Detect Platform

        private void DetectPlatform()
        {
            if (Platform != PlatformType.None &&
                Platform != PlatformType.Unknown)
            {
                // Manually overridden already
                return;
            }

            if (FindPlatformByFile(Root, out PlatformType platform))
            {
                // Check the possible files
                string? root = Path.GetDirectoryName(Root);
                if (string.IsNullOrEmpty(root))
                    throw new UserErrorException($"Error: Could not get root game folder from path: {Root}");

                Root = root;
                Platform = platform;
                return;
            }
            else if (Directory.Exists(Root))
            {
                // Check the possible directories for the possible files
                if (FindPlatformByFolder(ref Root, Path.Combine(Root, "PS3_GAME", "USRDIR"), out platform))
                {
                    Platform = platform;
                }
                else if (FindPlatformByFolder(ref Root, Path.Combine(Root, "USRDIR"), out platform))
                {
                    Platform = platform;
                }
                else if (FindPlatformByFolder(ref Root, Root, out platform))
                {
                    Platform = platform;
                }

                return;
            }

            throw new UserErrorException($"Error: Cannot determine {nameof(PlatformType)} from path: {Root}");
        }

        private static bool FindPlatformByFile(string file, out PlatformType platform)
        {
            if (File.Exists(file))
            {
                string name = Path.GetFileName(file);
                if (name.Equals("EBOOT.BIN", StringComparison.InvariantCultureIgnoreCase))
                {
                    platform = PlatformType.PlayStation3;
                    return true;
                }
                else if (name.EndsWith(".xex", StringComparison.InvariantCultureIgnoreCase))
                {
                    platform = PlatformType.Xbox360;
                    return true;
                }
                // Less likely
                else if (name.EndsWith(".elf", StringComparison.InvariantCultureIgnoreCase))
                {
                    platform = PlatformType.PlayStation3;
                    return true;
                }
            }

            platform = default;
            return false;
        }

        private static bool FindPlatformByFolder(ref string root, string folder, out PlatformType platform)
        {
            if (FindPlatformByFile(Path.Combine(folder, "EBOOT.BIN"), out platform))
            {
                root = folder;
                return true;
            }
            else if (FindPlatformByFile(Path.Combine(folder, "EBOOT.elf"), out platform))
            {
                root = folder;
                return true;
            }
            else if (FindPlatformByFile(Path.Combine(root, "default.xex"), out platform))
            {
                // Just checking in root here...
                return true;
            }

            return false;
        }

        #endregion

        #region Detect Root

        private void DetectRoot()
        {
            // Check the possible files
            if (CheckPlatformFileExists(Root, Platform))
            {
                string? root = Path.GetDirectoryName(Root);
                if (string.IsNullOrEmpty(root))
                    throw new UserErrorException($"Error: Could not get root game folder from path: {Root}");

                return;
            }
            else if (Directory.Exists(Root))
            {
                // Check the possible directories for the possible files
                if (CheckPlatformFolderExists(ref Root, Path.Combine(Root, "PS3_GAME", "USRDIR"), Platform))
                {
                    return;
                }
                else if (CheckPlatformFolderExists(ref Root, Path.Combine(Root, "USRDIR"), Platform))
                {
                    return;
                }
                else if (CheckPlatformFolderExists(ref Root, Root, Platform))
                {
                    return;
                }
            }

            throw new UserErrorException($"Cannot determine root path from {nameof(PlatformType)} {Platform} and path: {Root}");
        }

        private static bool CheckPlatformFileExists(string file, PlatformType platform)
        {
            if (File.Exists(file))
            {
                string name = Path.GetFileName(file);
                if (platform == PlatformType.PlayStation3)
                {
                    if (name.Equals("EBOOT.BIN", StringComparison.InvariantCultureIgnoreCase))
                    {
                        return true;
                    }
                    else if (name.EndsWith(".elf", StringComparison.InvariantCultureIgnoreCase))
                    {
                        return true;
                    }
                }
                else if (platform == PlatformType.Xbox360)
                {
                    if (name.EndsWith(".xex", StringComparison.InvariantCultureIgnoreCase))
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        private static bool CheckPlatformFolderExists(ref string root, string folder, PlatformType platform)
        {
            if (platform == PlatformType.PlayStation3)
            {
                if (CheckPlatformFileExists(Path.Combine(folder, "EBOOT.BIN"), platform))
                {
                    root = folder;
                    return true;
                }
                else if (CheckPlatformFileExists(Path.Combine(folder, "EBOOT.elf"), platform))
                {
                    root = folder;
                    return true;
                }
            }
            else if (platform == PlatformType.Xbox360)
            {
                if (CheckPlatformFileExists(Path.Combine(root, "default.xex"), platform))
                {
                    // Just checking in root here...
                    return true;
                }
            }

            return false;
        }

        #endregion

        #region Detect Game

        private void DetectGame()
        {
            if (Game != GameType.None &&
                Game != GameType.Unknown)
            {
                // Manually overridden already
                return;
            }

            Log.WriteLine("Attempting to determine game by checking platform...");
            Game = FindGameByPlatform(out RegionType region);

            // Simplify for now
            switch (region)
            {
                case RegionType.All:
                    region = RegionType.None;
                    break;
                case RegionType.Asia:
                    region = RegionType.Japan;
                    break;
            }

            if (region != RegionType.None ||
                region != RegionType.Unknown)
                Region = region;

            if (Game != GameType.Unknown && Game != GameType.None)
                return;

            // Determine which game we are loose loading for by files
            Log.WriteLine("Attempting to determine game by checking files...");
            Game = FindGameByFile();
            if (Game != GameType.Unknown && Game != GameType.None)
                return;

            throw new UserErrorException($"Error: Game could not be determined from {nameof(PlatformType)} {Platform} and path: {Root}");
        }

        private GameType FindGameByFile()
        {
            // TODO ACFA

            string bindDir = Path.Combine(Root, "bind");
            string acvPath = Path.Combine(bindDir, "dvdbnd.bdt");
            if (File.Exists(acvPath))
            {
                return GameType.ArmoredCoreV;
            }

            string acvdPath = Path.Combine(bindDir, "dvdbnd_layer0.bdt");
            if (File.Exists(acvdPath))
            {
                return GameType.ArmoredCoreVerdictDay;
            }

            return GameType.Unknown;
        }

        private GameType FindGameByPlatform(out RegionType region)
        {
            if (Platform == PlatformType.PlayStation3)
            {
                Log.WriteLine("Attempting to determine game by PARAM.SFO...");
                if (TryReadParamSfo(out PARAMSFO? sfo))
                {
                    return FindGameBySFO(sfo, out region);
                }
                else
                {
                    Log.WriteLine("Warning: PARAM.SFO could not be found or was invalid.");
                }
            }
            else if (Platform == PlatformType.Xbox360)
            {
                Log.WriteLine("Attempting to determine game by XEX...");
                XEX2? xex;
                string xexPath = Path.Combine(Root, "default.xex");
                string xexPath2 = Path.Combine(Root, "Release.xex");
                if (File.Exists(xexPath))
                {
                    xex = XEX2.Read(xexPath);
                }
                else if (File.Exists(xexPath2))
                {
                    xex = XEX2.Read(xexPath2);
                }
                else
                {
                    xex = null;
                }

                if (xex != null)
                {
                    region = XexHelper.GetRegionType(xex);
                    var game = FindGameByTitleID(XexHelper.GetTitleId(xex));
                    if (game == GameType.None ||
                        game == GameType.Unknown)
                    {
                        game = FindGameByOriginalPeName(XexHelper.GetOriginalPeName(xex));
                    }

                    return game;
                }
            }

            region = RegionType.None;
            return GameType.Unknown;
        }

        private static GameType FindGameBySFO(PARAMSFO sfo, out RegionType region)
        {
            // Try to find the title ID
            if (sfo.Parameters.TryGetValue("TITLE_ID", out PARAMSFO.Parameter? parameter))
            {
                switch (parameter.Data)
                {
                    case "BLJM55005":
                    case "BLJM60066":
                        region = RegionType.Japan;
                        return GameType.ArmoredCoreForAnswer;
                    case "BLUS30187":
                        region = RegionType.UnitedStates;
                        return GameType.ArmoredCoreForAnswer;
                    case "BLES00370":
                        region = RegionType.Europe;
                        return GameType.ArmoredCoreForAnswer;
                    case "BLKS20356":
                        region = RegionType.Korea;
                        return GameType.ArmoredCoreV;
                    case "BLAS50448":
                        region = RegionType.Asia;
                        return GameType.ArmoredCoreV;
                    case "BLJM60378":
                        region = RegionType.Japan;
                        return GameType.ArmoredCoreV;
                    case "BLUS30516":
                        region = RegionType.UnitedStates;
                        return GameType.ArmoredCoreV;
                    case "BLES01440":
                        region = RegionType.Europe;
                        return GameType.ArmoredCoreV;
                    case "BLKS20441":
                        region = RegionType.Korea;
                        return GameType.ArmoredCoreVerdictDay;
                    case "BLAS50618":
                        region = RegionType.Asia;
                        return GameType.ArmoredCoreVerdictDay;
                    case "BLJM61014":
                    case "BLJM61020":
                        region = RegionType.Japan;
                        return GameType.ArmoredCoreVerdictDay;
                    case "BLUS31194":
                    case "NPUB31245":
                        region = RegionType.UnitedStates;
                        return GameType.ArmoredCoreVerdictDay;
                    case "BLES01898":
                    case "NPEB01428":
                        region = RegionType.Europe;
                        return GameType.ArmoredCoreVerdictDay;
                }
            }

            // Try to find the title name
            if (sfo.Parameters.TryGetValue("TITLE", out parameter))
            {
                switch (parameter.Data)
                {
                    case "ARMORED CORE for Answer":
                        region = RegionType.None;
                        return GameType.ArmoredCoreForAnswer;
                    case "ARMORED CORE V":
                        region = RegionType.None;
                        return GameType.ArmoredCoreV;
                    case "Armored Core Verdict Day":
                    case "Armored Core™: Verdict Day™":
                        region = RegionType.None;
                        return GameType.ArmoredCoreVerdictDay;
                }
            }

            region = RegionType.None;
            return GameType.Unknown;
        }

        private static GameType FindGameByTitleID(string titleID)
        {
            switch (titleID)
            {
                case "FS2010":
                case "465307DA":
                    return GameType.ArmoredCoreForAnswer;
                case "NM2127":
                case "4E4D084F":
                    return GameType.ArmoredCoreV;
                // Also has the same title ID as ACV as an alternative title ID
                case "NM2131": // Alternative
                case "4E4D0853":
                case "NM2132": // Alternative
                case "4E4D0854":
                case "NM2148":
                case "4E4D0864":
                    return GameType.ArmoredCoreVerdictDay;
            }

            return GameType.Unknown;
        }

        private static GameType FindGameByOriginalPeName(string titleID)
        {
            switch (titleID)
            {
                case "AC45 - xenon.exe":
                    return GameType.ArmoredCoreForAnswer;
                case "ACV.exe":
                    return GameType.ArmoredCoreV;
                case "ACV2.exe":
                    return GameType.ArmoredCoreVerdictDay;
            }

            return GameType.Unknown;
        }

        private bool TryReadParamSfo([NotNullWhen(true)] out PARAMSFO? sfo)
        {
            // Get the USRDIR folder
            if (Root.EndsWith("USRDIR"))
            {
                // Get the PS3_GAME folder (disc) or root game folder (digital).
                string? parentDir = Path.GetDirectoryName(Root);
                if (!string.IsNullOrEmpty(parentDir))
                {
                    // Determine which game we are loose loading for by PARAM.SFO
                    string sfoPath = Path.Combine(parentDir, "PARAM.SFO");
                    if (File.Exists(sfoPath)
                        && PARAMSFO.IsRead(sfoPath, out sfo))
                    {
                        return true;
                    }
                }
            }

            sfo = null;
            return false;
        }

        #endregion
    }
}
