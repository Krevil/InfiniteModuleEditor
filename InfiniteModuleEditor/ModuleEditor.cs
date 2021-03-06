using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;
using System.Windows;
using System.Xml;
using System.Runtime.Serialization.Formatters.Binary;
using System.Runtime.InteropServices;
using OodleSharp;

namespace InfiniteModuleEditor
{
   public class ModuleEditor
    {
        public static Module ReadModule(FileStream fileStream)
        {
            byte[] ModuleHeader = new byte[72];
            fileStream.Read(ModuleHeader, 0, 72);
            Module module = new Module
            {
                Head = Encoding.ASCII.GetString(ModuleHeader, 0, 4),
                Version = BitConverter.ToInt32(ModuleHeader, 4),
                ModuleId = BitConverter.ToInt64(ModuleHeader, 8),
                FileCount = BitConverter.ToInt32(ModuleHeader, 16),
                ManifestCount = BitConverter.ToInt32(ModuleHeader, 20),
                ResourceIndex = BitConverter.ToInt32(ModuleHeader, 32),
                StringsSize = BitConverter.ToInt32(ModuleHeader, 36),
                ResourceCount = BitConverter.ToInt32(ModuleHeader, 40),
                BlockCount = BitConverter.ToInt32(ModuleHeader, 44)
            };
            module.StringTableOffset = module.FileCount * 88 + 72; //72 is header size
            module.ResourceListOffset = module.StringTableOffset + module.StringsSize + 8; //Still dunno why these 8 bytes are here
            module.BlockListOffset = module.ResourceCount * 4 + module.ResourceListOffset;
            module.FileDataOffset = module.BlockCount * 20 + module.BlockListOffset; //inaccurate, need to skip past a bunch of 00s

            int FileEntriesSize = module.FileCount * 88;
            byte[] ModuleFileEntries = new byte[FileEntriesSize];
            fileStream.Read(ModuleFileEntries, 0, FileEntriesSize);
            fileStream.Seek(8, SeekOrigin.Current); //No idea what these bytes are for
            byte[] ModuleStrings = new byte[module.StringsSize];
            fileStream.Read(ModuleStrings, 0, module.StringsSize);

            //To fix the data offset
            fileStream.Seek(module.FileDataOffset, SeekOrigin.Begin);
            while (fileStream.ReadByte() == 0)
            {
                continue;
            }
            module.FileDataOffset = fileStream.Position - 1;

            Dictionary<int, string> StringList = new Dictionary<int, string>();

            for (int i = 0; i < FileEntriesSize; i += 88)
            {
                ModuleFileEntry moduleItem = new ModuleFileEntry
                {
                    ResourceCount = BitConverter.ToInt32(ModuleFileEntries, i),
                    ParentIndex = BitConverter.ToInt32(ModuleFileEntries, i + 4), //Seems to always be 0
                    //unknown int16 8
                    BlockCount = BitConverter.ToInt16(ModuleFileEntries, i + 10),
                    BlockIndex = BitConverter.ToInt32(ModuleFileEntries, i + 12),
                    ResourceIndex = BitConverter.ToInt32(ModuleFileEntries, i + 16),
                    ClassId = BitConverter.ToInt32(ModuleFileEntries, i + 20),
                    DataOffset = BitConverter.ToUInt32(ModuleFileEntries, i + 24), //some special stuff needs to be done here, check back later
                    //unknown int16 30
                    TotalCompressedSize = BitConverter.ToUInt32(ModuleFileEntries, i + 32),
                    TotalUncompressedSize = BitConverter.ToUInt32(ModuleFileEntries, i + 36),
                    GlobalTagId = BitConverter.ToInt32(ModuleFileEntries, i + 40),
                    UncompressedHeaderSize = BitConverter.ToUInt32(ModuleFileEntries, i + 44),
                    UncompressedTagDataSize = BitConverter.ToUInt32(ModuleFileEntries, i + 48),
                    UncompressedResourceDataSize = BitConverter.ToUInt32(ModuleFileEntries, i + 52),
                    HeaderBlockCount = BitConverter.ToInt16(ModuleFileEntries, i + 56),
                    TagDataBlockCount = BitConverter.ToInt16(ModuleFileEntries, i + 58),
                    ResourceBlockCount = BitConverter.ToInt16(ModuleFileEntries, i + 60),
                    //padding
                    NameOffset = BitConverter.ToInt32(ModuleFileEntries, i + 64),
                    //unknown int32 68 //Seems to always be -1
                    AssetChecksum = BitConverter.ToInt64(ModuleFileEntries, i + 72),
                    AssetId = BitConverter.ToInt64(ModuleFileEntries, i + 80)
                };
                if (moduleItem.GlobalTagId == -1)
                {
                    continue;
                }
                ModuleFileEntry moduleItemNext = new ModuleFileEntry();
                string TagName;
                if (i + 88 != FileEntriesSize)
                {
                    moduleItemNext.NameOffset = BitConverter.ToInt32(ModuleFileEntries, i + 88 + 64);
                    TagName = Encoding.ASCII.GetString(ModuleStrings, moduleItem.NameOffset, moduleItemNext.NameOffset - moduleItem.NameOffset);
                }
                else
                {
                    TagName = Encoding.ASCII.GetString(ModuleStrings, moduleItem.NameOffset, module.StringsSize - moduleItem.NameOffset);
                }
                StringList.Add(moduleItem.GlobalTagId, TagName);
                module.ModuleFiles.Add(TagName, new ModuleFile { FileEntry = moduleItem });
            }
            return module;
        }

        public static MemoryStream GetTag(Module module, FileStream fileStream, string SearchTerm)
        {
            foreach (KeyValuePair<string, ModuleFile> moduleFile in module.ModuleFiles)
            {
                if (!moduleFile.Key.Contains(SearchTerm))
                {
                    continue;
                }
                if (moduleFile.Value.FileEntry.TotalUncompressedSize == 0)
                {
                    continue;
                }
                ulong FirstBlockOffset = moduleFile.Value.FileEntry.DataOffset + (ulong)module.FileDataOffset;
                MemoryStream outputStream = new MemoryStream();
                if (moduleFile.Value.FileEntry.BlockCount != 0)
                {
                    for (int y = 0; y < moduleFile.Value.FileEntry.BlockCount; y++)
                    {
                        byte[] BlockBuffer = new byte[20];
                        fileStream.Seek((moduleFile.Value.FileEntry.BlockIndex * 20) + module.BlockListOffset + (y * 20), 0);
                        fileStream.Read(BlockBuffer, 0, 20);
                        Block block = new Block
                        {
                            CompressedOffset = BitConverter.ToUInt32(BlockBuffer, 0),
                            CompressedSize = BitConverter.ToUInt32(BlockBuffer, 4),
                            UncompressedOffset = BitConverter.ToUInt32(BlockBuffer, 8),
                            UncompressedSize = BitConverter.ToUInt32(BlockBuffer, 12),
                            Compressed = BitConverter.ToBoolean(BlockBuffer, 16)
                        };

                        //This is where it gets ugly-er
                        byte[] BlockFile = new byte[block.CompressedSize];
                        ulong BlockOffset = FirstBlockOffset + block.CompressedOffset;
                        fileStream.Seek((long)BlockOffset, 0);
                        moduleFile.Value.Blocks.Add(new BlockInfo { BlockData = block, ModuleOffset = fileStream.Position, BlockType = y });
                        fileStream.Read(BlockFile, 0, (int)block.CompressedSize);
                        if (block.Compressed)
                        {
                            byte[] DecompressedFile = Oodle.Decompress(BlockFile, BlockFile.Length, (int)block.UncompressedSize);
                            outputStream.Write(DecompressedFile, 0, DecompressedFile.Length);
                        }
                        else //if the block file is uncompressed
                        {
                            outputStream.Write(BlockFile, 0, BlockFile.Length);
                        }
                    }
                }
                else
                {
                    byte[] CompressedFile = new byte[moduleFile.Value.FileEntry.TotalCompressedSize];
                    fileStream.Seek((int)moduleFile.Value.FileEntry.DataOffset, 0);
                    fileStream.Read(CompressedFile, 0, (int)moduleFile.Value.FileEntry.TotalCompressedSize);
                    byte[] DecompressedFile = Oodle.Decompress(CompressedFile, (int)moduleFile.Value.FileEntry.TotalCompressedSize, (int)moduleFile.Value.FileEntry.TotalUncompressedSize);
                    outputStream.Write(DecompressedFile, 0, DecompressedFile.Length);
                }

                return outputStream;
            }
            return null;
        }

        public static Tag ReadTag(MemoryStream TagStream, string ShortTagName, ModuleFile ModuleFile)
        {

            Tag tag = new Tag();
            byte[] TagHeader = new byte[80];


            //FileStream fileStream = new FileStream(FilePath, FileMode.Open);
            TagStream.Seek(0, SeekOrigin.Begin);
            TagStream.Read(TagHeader, 0, 80);


            GCHandle HeaderHandle = GCHandle.Alloc(TagHeader, GCHandleType.Pinned);
            tag.Header = (FileHeader)Marshal.PtrToStructure(HeaderHandle.AddrOfPinnedObject(), typeof(FileHeader)); //No idea how this magic bytes to structure stuff works, I just got this from github
            HeaderHandle.Free();

            tag.TagDependencyArray = new TagDependency[tag.Header.DependencyCount];
            tag.DataBlockArray = new DataBlock[tag.Header.DataBlockCount];
            tag.TagStructArray = new TagStruct[tag.Header.TagStructCount];
            tag.DataReferenceArray = new DataReference[tag.Header.DataReferenceCount];
            tag.TagReferenceFixupArray = new TagReferenceFixup[tag.Header.TagReferenceCount];
            //tag.StringIDArray = new StringID[tag.Header.StringIDCount]; //Not sure about the StringIDCount. Needs investigation
            tag.StringTable = new byte[tag.Header.StringTableSize];

            for (long l = 0; l < tag.Header.DependencyCount; l++) //For each tag dependency, fill in its values
            {
                byte[] TagDependencyBytes = new byte[Marshal.SizeOf(tag.TagDependencyArray[l])];
                TagStream.Read(TagDependencyBytes, 0, Marshal.SizeOf(tag.TagDependencyArray[l]));
                GCHandle TagDependencyHandle = GCHandle.Alloc(TagDependencyBytes, GCHandleType.Pinned);
                tag.TagDependencyArray[l] = (TagDependency)Marshal.PtrToStructure(TagDependencyHandle.AddrOfPinnedObject(), typeof(TagDependency));
                TagDependencyHandle.Free();
            }

            for (long l = 0; l < tag.Header.DataBlockCount; l++)
            {
                byte[] DataBlockBytes = new byte[Marshal.SizeOf(tag.DataBlockArray[l])];
                TagStream.Read(DataBlockBytes, 0, Marshal.SizeOf(tag.DataBlockArray[l]));
                GCHandle DataBlockHandle = GCHandle.Alloc(DataBlockBytes, GCHandleType.Pinned);
                tag.DataBlockArray[l] = (DataBlock)Marshal.PtrToStructure(DataBlockHandle.AddrOfPinnedObject(), typeof(DataBlock));
                DataBlockHandle.Free();
            }

            for (long l = 0; l < tag.Header.TagStructCount; l++)
            {
                byte[] TagStructBytes = new byte[Marshal.SizeOf(tag.TagStructArray[l])];
                TagStream.Read(TagStructBytes, 0, Marshal.SizeOf(tag.TagStructArray[l]));
                GCHandle TagStructHandle = GCHandle.Alloc(TagStructBytes, GCHandleType.Pinned);
                tag.TagStructArray[l] = (TagStruct)Marshal.PtrToStructure(TagStructHandle.AddrOfPinnedObject(), typeof(TagStruct));
                TagStructHandle.Free();
            }


            for (long l = 0; l < tag.Header.DataReferenceCount; l++)
            {
                byte[] DataReferenceBytes = new byte[Marshal.SizeOf(tag.DataReferenceArray[l])];
                TagStream.Read(DataReferenceBytes, 0, Marshal.SizeOf(tag.DataReferenceArray[l]));
                GCHandle DataReferenceHandle = GCHandle.Alloc(DataReferenceBytes, GCHandleType.Pinned);
                tag.DataReferenceArray[l] = (DataReference)Marshal.PtrToStructure(DataReferenceHandle.AddrOfPinnedObject(), typeof(DataReference));
                DataReferenceHandle.Free();
            }

            for (long l = 0; l < tag.Header.TagReferenceCount; l++)
            {
                byte[] TagReferenceBytes = new byte[Marshal.SizeOf(tag.TagReferenceFixupArray[l])];
                TagStream.Read(TagReferenceBytes, 0, Marshal.SizeOf(tag.TagReferenceFixupArray[l]));
                GCHandle TagReferenceHandle = GCHandle.Alloc(TagReferenceBytes, GCHandleType.Pinned);
                tag.TagReferenceFixupArray[l] = (TagReferenceFixup)Marshal.PtrToStructure(TagReferenceHandle.AddrOfPinnedObject(), typeof(TagReferenceFixup));
                TagReferenceHandle.Free();
            }

            //Not sure about this stuff, might not be in every tag?
            /*
            if (tag.Header.ZoneSetDataSize > 1)
            {
                byte[] ZoneSetHeader = new byte[16];
                fileStream.Read(ZoneSetHeader, 0, 16);
                GCHandle ZoneSetHandle = GCHandle.Alloc(ZoneSetHeader, GCHandleType.Pinned);
                tag.ZoneSetInfoHeader = (ZoneSetInformationHeader)Marshal.PtrToStructure(ZoneSetHandle.AddrOfPinnedObject(), typeof(ZoneSetInformationHeader));
                ZoneSetHandle.Free();

                tag.ZoneSetEntryArray = new ZoneSetEntry[tag.ZoneSetInfoHeader.ZoneSetCount];
                long ZoneSetTagCount = 0;
                foreach (ZoneSetEntry zse in tag.ZoneSetEntryArray)
                {
                    ZoneSetTagCount += zse.TagCount;
                }
                tag.ZoneSetTagArray = new ZoneSetTag[ZoneSetTagCount];
            }
            */

            //TagStream.Seek(tag.Header.HeaderSize, SeekOrigin.Begin); //just to be sure
            TagStream.Read(tag.StringTable, 0, (int)tag.Header.StringTableSize); //better hope this never goes beyond sizeof(int)
            TagStream.Seek(tag.Header.ZoneSetDataSize, SeekOrigin.Current); //Data starts here after the "StringID" section which is probably something else

            //hacky fix for biped tags and similar to skip over unknown data that is considered part of the tag for some reason
            int CurrentOffset = (int)TagStream.Position;
            int TagDataOffset = (int)TagStream.Position;
            byte[] TempBuffer = new byte[4];
            TagStream.Read(TempBuffer, 0, 4);
            while (BitConverter.ToInt32(TempBuffer, 0) != ModuleFile.FileEntry.GlobalTagId)
            {
                TagStream.Read(TempBuffer, 0, 4);
            }
            TagStream.Seek(-12, SeekOrigin.Current);
            TagDataOffset = (int)TagStream.Position - TagDataOffset;
            tag.TrueDataOffset = TagDataOffset;
            TagStream.Seek(CurrentOffset, SeekOrigin.Begin);
            System.Diagnostics.Debug.WriteLine("{0} {1} {2}", CurrentOffset, ModuleFile.FileEntry.GlobalTagId, TagDataOffset);
            tag.TagData = new byte[tag.Header.DataSize];
            TagStream.Read(tag.TagData, 0, (int)tag.Header.DataSize);

            tag.MainStructSize = 0;
            tag.TotalTagBlockDataSize = 0;

            for (int i = 0; i < tag.DataBlockArray.Length; i++)
            {
                tag.TotalTagBlockDataSize += (int)tag.DataBlockArray[i].Size;
            }

            tag.MainStructSize = (int)(tag.Header.DataSize - tag.TotalTagBlockDataSize);

            PluginReader pluginReader = new PluginReader();
            bool LoadXML = true;
            string PluginToLoad = "Plugins\\" + Utilities.GetClassID(ModuleFile.FileEntry.ClassId) + Path.GetExtension(ShortTagName).Substring(1) + ".xml";
            if (!File.Exists(PluginToLoad))
            {
                if (File.Exists("Plugins\\" + Utilities.GetClassID(ModuleFile.FileEntry.ClassId) + ".xml"))
                    PluginToLoad = "Plugins\\" + Utilities.GetClassID(ModuleFile.FileEntry.ClassId) + ".xml";
                else LoadXML = false;
            }
            System.Diagnostics.Debug.WriteLine("Loading " + PluginToLoad);
            List <PluginItem> PluginItems = LoadXML ? pluginReader.LoadPlugin(PluginToLoad, tag, TagDataOffset) : pluginReader.LoadGenericTag(tag, TagDataOffset);

            GetTagValues(PluginItems, tag);

            tag.TagValues = PluginItems;

            return tag;
        }

        public static Tag ReadTag(FileStream TagStream, string ShortTagName)
        {

            Tag tag = new Tag();
            byte[] TagHeader = new byte[80];


            //FileStream fileStream = new FileStream(FilePath, FileMode.Open);
            TagStream.Seek(0, SeekOrigin.Begin);
            TagStream.Read(TagHeader, 0, 80);


            GCHandle HeaderHandle = GCHandle.Alloc(TagHeader, GCHandleType.Pinned);
            tag.Header = (FileHeader)Marshal.PtrToStructure(HeaderHandle.AddrOfPinnedObject(), typeof(FileHeader)); //No idea how this magic bytes to structure stuff works, I just got this from github
            HeaderHandle.Free();

            tag.TagDependencyArray = new TagDependency[tag.Header.DependencyCount];
            tag.DataBlockArray = new DataBlock[tag.Header.DataBlockCount];
            tag.TagStructArray = new TagStruct[tag.Header.TagStructCount];
            tag.DataReferenceArray = new DataReference[tag.Header.DataReferenceCount];
            tag.TagReferenceFixupArray = new TagReferenceFixup[tag.Header.TagReferenceCount];
            //tag.StringIDArray = new StringID[tag.Header.StringIDCount]; //Not sure about the StringIDCount. Needs investigation
            tag.StringTable = new byte[tag.Header.StringTableSize];

            for (long l = 0; l < tag.Header.DependencyCount; l++) //For each tag dependency, fill in its values
            {
                byte[] TagDependencyBytes = new byte[Marshal.SizeOf(tag.TagDependencyArray[l])];
                TagStream.Read(TagDependencyBytes, 0, Marshal.SizeOf(tag.TagDependencyArray[l]));
                GCHandle TagDependencyHandle = GCHandle.Alloc(TagDependencyBytes, GCHandleType.Pinned);
                tag.TagDependencyArray[l] = (TagDependency)Marshal.PtrToStructure(TagDependencyHandle.AddrOfPinnedObject(), typeof(TagDependency));
                TagDependencyHandle.Free();
            }

            for (long l = 0; l < tag.Header.DataBlockCount; l++)
            {
                byte[] DataBlockBytes = new byte[Marshal.SizeOf(tag.DataBlockArray[l])];
                TagStream.Read(DataBlockBytes, 0, Marshal.SizeOf(tag.DataBlockArray[l]));
                GCHandle DataBlockHandle = GCHandle.Alloc(DataBlockBytes, GCHandleType.Pinned);
                tag.DataBlockArray[l] = (DataBlock)Marshal.PtrToStructure(DataBlockHandle.AddrOfPinnedObject(), typeof(DataBlock));
                DataBlockHandle.Free();
            }

            for (long l = 0; l < tag.Header.TagStructCount; l++)
            {
                byte[] TagStructBytes = new byte[Marshal.SizeOf(tag.TagStructArray[l])];
                TagStream.Read(TagStructBytes, 0, Marshal.SizeOf(tag.TagStructArray[l]));
                GCHandle TagStructHandle = GCHandle.Alloc(TagStructBytes, GCHandleType.Pinned);
                tag.TagStructArray[l] = (TagStruct)Marshal.PtrToStructure(TagStructHandle.AddrOfPinnedObject(), typeof(TagStruct));
                TagStructHandle.Free();
            }


            for (long l = 0; l < tag.Header.DataReferenceCount; l++)
            {
                byte[] DataReferenceBytes = new byte[Marshal.SizeOf(tag.DataReferenceArray[l])];
                TagStream.Read(DataReferenceBytes, 0, Marshal.SizeOf(tag.DataReferenceArray[l]));
                GCHandle DataReferenceHandle = GCHandle.Alloc(DataReferenceBytes, GCHandleType.Pinned);
                tag.DataReferenceArray[l] = (DataReference)Marshal.PtrToStructure(DataReferenceHandle.AddrOfPinnedObject(), typeof(DataReference));
                DataReferenceHandle.Free();
            }

            for (long l = 0; l < tag.Header.TagReferenceCount; l++)
            {
                byte[] TagReferenceBytes = new byte[Marshal.SizeOf(tag.TagReferenceFixupArray[l])];
                TagStream.Read(TagReferenceBytes, 0, Marshal.SizeOf(tag.TagReferenceFixupArray[l]));
                GCHandle TagReferenceHandle = GCHandle.Alloc(TagReferenceBytes, GCHandleType.Pinned);
                tag.TagReferenceFixupArray[l] = (TagReferenceFixup)Marshal.PtrToStructure(TagReferenceHandle.AddrOfPinnedObject(), typeof(TagReferenceFixup));
                TagReferenceHandle.Free();
            }

            //Not sure about this stuff, might not be in every tag?
            /*
            if (tag.Header.ZoneSetDataSize > 1)
            {
                byte[] ZoneSetHeader = new byte[16];
                fileStream.Read(ZoneSetHeader, 0, 16);
                GCHandle ZoneSetHandle = GCHandle.Alloc(ZoneSetHeader, GCHandleType.Pinned);
                tag.ZoneSetInfoHeader = (ZoneSetInformationHeader)Marshal.PtrToStructure(ZoneSetHandle.AddrOfPinnedObject(), typeof(ZoneSetInformationHeader));
                ZoneSetHandle.Free();

                tag.ZoneSetEntryArray = new ZoneSetEntry[tag.ZoneSetInfoHeader.ZoneSetCount];
                long ZoneSetTagCount = 0;
                foreach (ZoneSetEntry zse in tag.ZoneSetEntryArray)
                {
                    ZoneSetTagCount += zse.TagCount;
                }
                tag.ZoneSetTagArray = new ZoneSetTag[ZoneSetTagCount];
            }
            */

            TagStream.Read(tag.StringTable, 0, (int)tag.Header.StringTableSize); //better hope this never goes beyond sizeof(int)
            TagStream.Seek(tag.Header.ZoneSetDataSize, SeekOrigin.Current); //Data starts here after the "StringID" section which is probably something else

            //hacky fix for biped tags and similar to skip over unknown data that is considered part of the tag for some reason
            /* won't work if we don't know the global tag id
            int CurrentOffset = (int)TagStream.Position;
            int TagDataOffset = (int)TagStream.Position;
            byte[] TempBuffer = new byte[4];
            TagStream.Read(TempBuffer, 0, 4);
            while (BitConverter.ToInt32(TempBuffer, 0) != GlobalTagId)
            {
                TagStream.Read(TempBuffer, 0, 4);
            }
            TagStream.Seek(-12, SeekOrigin.Current);
            TagDataOffset = (int)TagStream.Position - TagDataOffset;
            tag.TrueDataOffset = TagDataOffset;
            TagStream.Seek(CurrentOffset, SeekOrigin.Begin);
            System.Diagnostics.Debug.WriteLine("{0} {1} {2}", CurrentOffset, GlobalTagId, TagDataOffset);
            */
            int TagDataOffset = 0;
            if (Path.GetExtension(ShortTagName) == ".biped") //hackier fix. better hope all biped tags have this at the same size
            {
                TagDataOffset += 22172;
            }
            tag.TagData = new byte[tag.Header.DataSize];
            TagStream.Read(tag.TagData, 0, (int)tag.Header.DataSize);

            tag.MainStructSize = 0;
            tag.TotalTagBlockDataSize = 0;

            for (int i = 0; i < tag.DataBlockArray.Length; i++)
            {
                tag.TotalTagBlockDataSize += (int)tag.DataBlockArray[i].Size;
            }

            tag.MainStructSize = (int)(tag.Header.DataSize - tag.TotalTagBlockDataSize);

            PluginReader pluginReader = new PluginReader();
            bool LoadXML = true;
            /* doesn't work without classid
            string PluginToLoad = "Plugins\\" + Utilities.GetClassID(ClassId) + Path.GetExtension(ShortTagName).Substring(1) + ".xml";
            if (!File.Exists(PluginToLoad))
            {
                if (File.Exists("Plugins\\" + Utilities.GetClassID(ClassId) + ".xml"))
                    PluginToLoad = "Plugins\\" + Utilities.GetClassID(ClassId) + ".xml";
                else LoadXML = false;
            }
            */
            string PluginToLoad = "Plugins\\";
            switch (Path.GetExtension(ShortTagName))
            {
                case ".grapplehookdefinitiontag":
                    PluginToLoad += "saghgrapplehookdefinitiontag.xml";
                break;
                case ".biped":
                    PluginToLoad += "bipdbiped.xml";
                break;
                case ".model":
                    PluginToLoad += "hlmtmodel.xml";
                break;
                case ".projectile":
                    PluginToLoad += "projprojectile.xml";
                break;
                case ".weapon":
                    PluginToLoad += "weapweapon.xml";
                break;
                default:
                    MessageBox.Show("Couldn't find a suitable plugin for tag " + ShortTagName + "\nUsing generic fields to display tag");
                LoadXML = false;
                break;
            }
            System.Diagnostics.Debug.WriteLine("Loading " + PluginToLoad);
            List<PluginItem> PluginItems = LoadXML ? pluginReader.LoadPlugin(PluginToLoad, tag, TagDataOffset) : pluginReader.LoadGenericTag(tag, TagDataOffset);

            GetTagValues(PluginItems, tag);

            tag.TagValues = PluginItems;

            return tag;
        }

        public static void GetTagValues(List<PluginItem> PluginItems, Tag tag)
        {
            foreach (PluginItem Item in PluginItems)
            {
                switch (Item.FieldType)
                {
                    case PluginField.Real:
                        Item.Value = BitConverter.ToSingle(tag.TagData, Item.Offset);
                        break;
                    case PluginField.StringID:
                    case PluginField.Int32:
                        Item.Value = BitConverter.ToUInt32(tag.TagData, Item.Offset);
                        break;
                    case PluginField.Int16:
                        Item.Value = BitConverter.ToUInt16(tag.TagData, Item.Offset);
                        break;
                    case PluginField.Int8:
                        Item.Value = tag.TagData[Item.Offset];
                        break;
                    case PluginField.TagReference:
                        Item.Value = new TagReference
                        {
                            TypeInfo = BitConverter.ToUInt64(tag.TagData, Item.Offset),
                            GlobalID = BitConverter.ToInt32(tag.TagData, Item.Offset + 8),
                            AssetID = BitConverter.ToInt64(tag.TagData, Item.Offset + 12),
                            GroupTag = BitConverter.ToInt32(tag.TagData, Item.Offset + 20),
                            LocalHandle = BitConverter.ToInt32(tag.TagData, Item.Offset + 24)
                        };
                        break;
                    case PluginField.DataReference:
                        Item.Value = new DataReferenceField
                        {
                            Data = BitConverter.ToUInt64(tag.TagData, Item.Offset),
                            TypeInfo = BitConverter.ToUInt64(tag.TagData, Item.Offset + 8),
                            UnknownProperty = BitConverter.ToInt32(tag.TagData, Item.Offset + 16),
                            Size = BitConverter.ToUInt32(tag.TagData, Item.Offset + 20)
                        };
                        break;
                    case PluginField.ShortBounds:
                        Item.Value = new ShortBounds
                        {
                            MinBound = BitConverter.ToInt16(tag.TagData, Item.Offset),
                            MaxBound = BitConverter.ToInt16(tag.TagData, Item.Offset + 2)
                        };
                        break;
                    case PluginField.RealBounds:
                        Item.Value = new RealBounds
                        {
                            MinBound = BitConverter.ToSingle(tag.TagData, Item.Offset),
                            MaxBound = BitConverter.ToSingle(tag.TagData, Item.Offset + 4)
                        };
                        break;
                    case PluginField.Vector3D:
                        Item.Value = new RealVector3D
                        {
                            I = BitConverter.ToSingle(tag.TagData, Item.Offset),
                            J = BitConverter.ToSingle(tag.TagData, Item.Offset + 4),
                            K = BitConverter.ToSingle(tag.TagData, Item.Offset + 8)
                        };
                        break;
                    case PluginField.Flags8:
                        Item.Value = tag.TagData[Item.Offset];
                        break;
                    case PluginField.Flags16:
                        Item.Value = BitConverter.ToUInt16(tag.TagData, Item.Offset);
                        break;
                    case PluginField.Flags32:
                        Item.Value = BitConverter.ToUInt32(tag.TagData, Item.Offset);
                        break;
                    case PluginField.Enum8:
                        Item.Value = tag.TagData[Item.Offset];
                        break;
                    case PluginField.Enum16:
                        Item.Value = BitConverter.ToUInt16(tag.TagData, Item.Offset);
                        break;
                    case PluginField.Enum32:
                        Item.Value = BitConverter.ToUInt32(tag.TagData, Item.Offset);
                        break;
                    case PluginField.TagBlock:
                        Item.Value = BitConverter.ToUInt32(tag.TagData, Item.Offset + 16);
                        break;
                    default:
                        break;
                }
            }
        }

        public static bool WriteTag(ModuleFile ModuleFile, MemoryStream TagStream, FileStream ModuleStream)
        {
            bool WriteTagHeader = false;
            foreach (PluginItem Item in ModuleFile.Tag.TagValues)
            {
                if (Item.GetModified())
                {
                    TagStream.Seek(Item.Offset + ModuleFile.Tag.Header.HeaderSize, SeekOrigin.Begin);
                    switch (Item.FieldType)
                    {
                        case PluginField.Real:
                            TagStream.Write(BitConverter.GetBytes(Convert.ToSingle(Item.Value)), 0, 4);
                            break;
                        case PluginField.StringID:
                        case PluginField.Int32:
                        case PluginField.Flags32:
                        case PluginField.Enum32:
                            TagStream.Write(BitConverter.GetBytes(Convert.ToUInt32(Item.Value)), 0, 4);
                            break;
                        case PluginField.Int16:
                        case PluginField.Flags16:
                        case PluginField.Enum16:
                            TagStream.Write(BitConverter.GetBytes(Convert.ToUInt16(Item.Value)), 0, 2);
                            break;
                        case PluginField.Enum8:
                        case PluginField.Int8:
                        case PluginField.Flags8:
                            TagStream.WriteByte(Convert.ToByte(Item.Value));
                            break;
                        case PluginField.TagReference:
                            //needs testing
                            WriteTagHeader = true;
                            TagReference TagRef = new TagReference { GlobalID = Convert.ToInt32((Item.Value as string).Split(' ')[0]), AssetID = Convert.ToInt64((Item.Value as string).Split(' ')[1]), GroupTag = Convert.ToInt32((Item.Value as string).Split(' ')[2]), TypeInfo = 0, LocalHandle = 0 };
                            TagStream.Seek(8, SeekOrigin.Current);
                            TagStream.Write(BitConverter.GetBytes(Convert.ToInt32(TagRef.GlobalID)), 0, 4);
                            TagStream.Write(BitConverter.GetBytes(Convert.ToInt64(TagRef.AssetID)), 0, 8);
                            TagStream.Write(BitConverter.GetBytes(Convert.ToInt32(TagRef.GroupTag)), 0, 4);
                            //tags don't seem to care what's in the data so we have to write the header
                            TagStream.Seek((ModuleFile.Tag.TagReferenceFixupArray.ToList().Find(x => x.FieldOffset == Item.Offset + ModuleFile.Tag.Header.HeaderSize).DepdencyIndex * 24) + 80, SeekOrigin.Begin);
                            TagStream.Write(BitConverter.GetBytes(Convert.ToInt32(TagRef.GroupTag)), 0, 4);
                            TagStream.Seek(4, SeekOrigin.Current);
                            TagStream.Write(BitConverter.GetBytes(Convert.ToInt64(TagRef.AssetID)), 0, 8);
                            TagStream.Write(BitConverter.GetBytes(Convert.ToInt32(TagRef.GlobalID)), 0, 4);
                            //and that doesn't seem to work either ?????????????????????????
                            break;
                        case PluginField.DataReference:
                            DataReferenceField DataRef = (DataReferenceField)Item.Value;
                            TagStream.Seek(20, SeekOrigin.Current);
                            TagStream.Write(BitConverter.GetBytes(Convert.ToInt32(DataRef.Size)), 0, 4);
                            break;
                        case PluginField.ShortBounds:
                            ShortBounds Int16Bounds = new ShortBounds { MinBound = Convert.ToInt16((Item.Value as string).Split(' ')[0]), MaxBound = Convert.ToInt16((Item.Value as string).Split(' ')[1]) };
                            TagStream.Write(BitConverter.GetBytes(Convert.ToSingle(Int16Bounds.MinBound)), 0, 2);
                            TagStream.Write(BitConverter.GetBytes(Convert.ToSingle(Int16Bounds.MaxBound)), 0, 2);
                            break;
                        case PluginField.RealBounds:
                            RealBounds FloatBounds = new RealBounds { MinBound = Convert.ToSingle((Item.Value as string).Split(' ')[0]), MaxBound = Convert.ToSingle((Item.Value as string).Split(' ')[1]) };
                            TagStream.Write(BitConverter.GetBytes(Convert.ToSingle(FloatBounds.MinBound)), 0, 4);
                            TagStream.Write(BitConverter.GetBytes(Convert.ToSingle(FloatBounds.MaxBound)), 0, 4);
                            break;
                        case PluginField.Vector3D:
                            RealVector3D Vector = new RealVector3D { I = Convert.ToSingle((Item.Value as string).Split(' ')[0]), J = Convert.ToSingle((Item.Value as string).Split(' ')[1]), K = Convert.ToSingle((Item.Value as string).Split(' ')[2]) };
                            TagStream.Write(BitConverter.GetBytes(Convert.ToSingle(Vector.I)), 0, 4);
                            TagStream.Write(BitConverter.GetBytes(Convert.ToSingle(Vector.J)), 0, 4);
                            TagStream.Write(BitConverter.GetBytes(Convert.ToSingle(Vector.K)), 0, 4);
                            break;
                        default:
                            MessageBox.Show("Unrecognized field type " + Item.FieldType + " in Item " + Item.Name + " at offset " + Item.Offset);
                            break;
                    }
                }
            }
            
            if (WriteTagHeader)
            {
                byte[] ModifiedHeader = new byte[ModuleFile.Tag.Header.HeaderSize];
                TagStream.Seek(0, SeekOrigin.Begin);
                TagStream.Read(ModifiedHeader, 0, (int)ModuleFile.Tag.Header.HeaderSize);

                byte[] CompressedModifiedHeader = Oodle.Compress(ModifiedHeader, ModifiedHeader.Length, OodleFormat.Kraken, OodleCompressionLevel.Optimal5); //Set to optimal because a smaller file can be put back in but a bigger one is no bueno
                if (CompressedModifiedHeader.Length <= ModuleFile.Blocks[0].BlockData.CompressedSize)
                {
                    ModuleStream.Seek(ModuleFile.Blocks[0].ModuleOffset, SeekOrigin.Begin);
                    ModuleStream.Write(CompressedModifiedHeader, 0, CompressedModifiedHeader.Length);
                }
                else return false;
            }         

            byte[] ModifiedTag = new byte[ModuleFile.Tag.Header.DataSize];
            TagStream.Seek(ModuleFile.Tag.Header.HeaderSize, SeekOrigin.Begin);
            TagStream.Read(ModifiedTag, 0, (int)ModuleFile.Tag.Header.DataSize);

            byte[] CompressedModifiedTag = Oodle.Compress(ModifiedTag, ModifiedTag.Length, OodleFormat.Kraken, OodleCompressionLevel.Optimal5); //Set to optimal because a smaller file can be put back in but a bigger one is no bueno

            if (CompressedModifiedTag.Length <= ModuleFile.Blocks[1].BlockData.CompressedSize)
            {
                ModuleStream.Seek(ModuleFile.Blocks[1].ModuleOffset, SeekOrigin.Begin);
                ModuleStream.Write(CompressedModifiedTag, 0, CompressedModifiedTag.Length);
            }
            else return false;

            return true;
        }

        public static bool WriteTag(FileStream TagStream, Tag Tag)
        {
            foreach (PluginItem Item in Tag.TagValues)
            {
                if (Item.GetModified())
                {
                    TagStream.Seek(Item.Offset + Tag.Header.HeaderSize, SeekOrigin.Begin);
                    switch (Item.FieldType)
                    {
                        case PluginField.Real:
                            TagStream.Write(BitConverter.GetBytes(Convert.ToSingle(Item.Value)), 0, 4);
                            break;
                        case PluginField.StringID:
                        case PluginField.Int32:
                        case PluginField.Flags32:
                        case PluginField.Enum32:
                            TagStream.Write(BitConverter.GetBytes(Convert.ToUInt32(Item.Value)), 0, 4);
                            break;
                        case PluginField.Int16:
                        case PluginField.Flags16:
                        case PluginField.Enum16:
                            TagStream.Write(BitConverter.GetBytes(Convert.ToUInt16(Item.Value)), 0, 2);
                            break;
                        case PluginField.Enum8:
                        case PluginField.Int8:
                        case PluginField.Flags8:
                            TagStream.WriteByte(Convert.ToByte(Item.Value));
                            break;
                        case PluginField.TagReference:
                            //needs testing
                            TagReference TagRef = new TagReference { GlobalID = Convert.ToInt32((Item.Value as string).Split(' ')[0]), AssetID = Convert.ToInt64((Item.Value as string).Split(' ')[1]), GroupTag = Convert.ToInt32((Item.Value as string).Split(' ')[2]), TypeInfo = 0, LocalHandle = 0 };
                            TagStream.Seek(8, SeekOrigin.Current);
                            TagStream.Write(BitConverter.GetBytes(Convert.ToInt32(TagRef.GlobalID)), 0, 4);
                            TagStream.Write(BitConverter.GetBytes(Convert.ToInt64(TagRef.AssetID)), 0, 8);
                            TagStream.Write(BitConverter.GetBytes(Convert.ToInt32(TagRef.GroupTag)), 0, 4);
                            //tags don't seem to care what's in the data so we have to write the header
                            TagStream.Seek((Tag.TagReferenceFixupArray.ToList().Find(x => x.FieldOffset == Item.Offset + Tag.Header.HeaderSize).DepdencyIndex * 24) + 80, SeekOrigin.Begin);
                            TagStream.Write(BitConverter.GetBytes(Convert.ToInt32(TagRef.GroupTag)), 0, 4);
                            TagStream.Seek(4, SeekOrigin.Current);
                            TagStream.Write(BitConverter.GetBytes(Convert.ToInt64(TagRef.AssetID)), 0, 8);
                            TagStream.Write(BitConverter.GetBytes(Convert.ToInt32(TagRef.GlobalID)), 0, 4);
                            //and that doesn't seem to work either ?????????????????????????
                            break;
                        case PluginField.DataReference:
                            DataReferenceField DataRef = (DataReferenceField)Item.Value;
                            TagStream.Seek(20, SeekOrigin.Current);
                            TagStream.Write(BitConverter.GetBytes(Convert.ToInt32(DataRef.Size)), 0, 4);
                            break;
                        case PluginField.ShortBounds:
                            ShortBounds Int16Bounds = new ShortBounds { MinBound = Convert.ToInt16((Item.Value as string).Split(' ')[0]), MaxBound = Convert.ToInt16((Item.Value as string).Split(' ')[1]) };
                            TagStream.Write(BitConverter.GetBytes(Convert.ToSingle(Int16Bounds.MinBound)), 0, 2);
                            TagStream.Write(BitConverter.GetBytes(Convert.ToSingle(Int16Bounds.MaxBound)), 0, 2);
                            break;
                        case PluginField.RealBounds:
                            RealBounds FloatBounds = new RealBounds { MinBound = Convert.ToSingle((Item.Value as string).Split(' ')[0]), MaxBound = Convert.ToSingle((Item.Value as string).Split(' ')[1]) };
                            TagStream.Write(BitConverter.GetBytes(Convert.ToSingle(FloatBounds.MinBound)), 0, 4);
                            TagStream.Write(BitConverter.GetBytes(Convert.ToSingle(FloatBounds.MaxBound)), 0, 4);
                            break;
                        case PluginField.Vector3D:
                            RealVector3D Vector = new RealVector3D { I = Convert.ToSingle((Item.Value as string).Split(' ')[0]), J = Convert.ToSingle((Item.Value as string).Split(' ')[1]), K = Convert.ToSingle((Item.Value as string).Split(' ')[2]) };
                            TagStream.Write(BitConverter.GetBytes(Convert.ToSingle(Vector.I)), 0, 4);
                            TagStream.Write(BitConverter.GetBytes(Convert.ToSingle(Vector.J)), 0, 4);
                            TagStream.Write(BitConverter.GetBytes(Convert.ToSingle(Vector.K)), 0, 4);
                            break;
                        default:
                            MessageBox.Show("Unrecognized field type " + Item.FieldType + " in Item " + Item.Name + " at offset " + Item.Offset);
                            break;
                    }
                }
            }

            return true;
        }

        public static void WriteTagInfo(string FilePath, Tag tag, List<PluginItem> PluginItems)
        {
            StreamWriter TextOutput = new StreamWriter(Path.GetFileName(FilePath) + ".fileinfo" + ".txt")
            {
                AutoFlush = true //Otherwise it caps at 4096 bytes unless you flush manually
            };
            TextOutput.WriteLine("File Header:");
            TextOutput.WriteLine();
            foreach (var a in tag.Header.GetType().GetFields())
            {
                TextOutput.WriteLine("{0} : {1}", a.Name, a.GetValue(tag.Header));
            }
            TextOutput.WriteLine();
            TextOutput.WriteLine("Tag Depdendencies:");
            TextOutput.WriteLine();
            Utilities.WriteObjectInfo(TextOutput, tag.TagDependencyArray, "Tag Dependency");
            TextOutput.WriteLine("Data Blocks:");
            TextOutput.WriteLine();
            Utilities.WriteObjectInfo(TextOutput, tag.DataBlockArray, "Data Block");
            TextOutput.WriteLine("Tag Structs:");
            TextOutput.WriteLine();
            Utilities.WriteObjectInfo(TextOutput, tag.TagStructArray, "Tag Struct");
            TextOutput.WriteLine("Data References:");
            TextOutput.WriteLine();
            Utilities.WriteObjectInfo(TextOutput, tag.DataReferenceArray, "Data Reference");
            TextOutput.WriteLine("Tag Reference Fixups:");
            TextOutput.WriteLine();
            Utilities.WriteObjectInfo(TextOutput, tag.TagReferenceFixupArray, "Tag Reference Fixup");
            TextOutput.WriteLine();
            TextOutput.WriteLine("Tag Data:");
            TextOutput.WriteLine();
            foreach (PluginItem Item in PluginItems)
            {
                //if (Item.Value != null)
                TextOutput.WriteLine(Item);
            }
        }
    }
}
