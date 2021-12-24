using System;
using System.IO;
using System.Text;
using System.Xml;
using System.Collections.Generic;
using System.Runtime.Serialization.Formatters.Binary;
using System.Runtime.InteropServices;
using OodleSharp;


namespace InfiniteModuleEditor
{
    public class PluginReader
    {
        public void RemoveBlocks(XmlDocument PluginXml, Tag Tag, int Offset)
        {
            PluginXml.Load("tempPlugin.xml");
            bool Removed = false;
            XmlNodeList AllNodes = PluginXml.SelectNodes("//*");
            int Position = Offset;
            for (int i = 0; i < AllNodes.Count; i++)
            {
                switch (AllNodes[i].Name.ToLower())
                {
                    case "_field_block_64":
                    case "_field_block_v2":
                        for (int y = 0; y < AllNodes[i].ChildNodes.Count; y++)
                        {
                            AllNodes[i].RemoveChild(AllNodes[i].ChildNodes[y]);
                            Removed = true;
                        }
                        Position += 20;
                        break;
                    default:
                        break;
                }
            }

            PluginXml.Save("tempPlugin.xml");
            if (Removed)
            {
                RemoveBlocks(PluginXml, Tag, Offset);
            }
        }

        public List<PluginItem> GenerateBlockValuesXML(string PluginPath, Tag Tag, List<PluginItem> PluginItems, int Offset)
        {
            XmlDocument PluginXml = new XmlDocument();
            PluginXml.Load(PluginPath);
            PluginXml.Save("tempPlugin.xml");
            RemoveBlocks(PluginXml, Tag, Offset);
            PluginXml.Load("tempPlugin.xml");
            XmlDocument NewXml = new XmlDocument();
            NewXml.LoadXml("<plugin>" + "</plugin>");
            bool ChildBlocks = false;
            Dictionary<int, string> BlocksToKeep = new Dictionary<int, string>();
            XmlNodeList AllNodes = PluginXml.SelectNodes("//*");//PluginXml.GetElementsByTagName("_field_block_v2");
            int Position = Offset;
            for (int i = 0; i < AllNodes.Count; i++)
            {
                switch (AllNodes[i].Name.ToLower())
                {
                    case "plugin":
                    case "item":
                        break;
                    case "_field_pad":
                    case "_field_skip":
                        Position += int.Parse(AllNodes[i].Attributes.GetNamedItem("length").Value);
                        break;
                    case "_field_block_64":
                    case "_field_block_v2":
                        if (Tag.TagBlockAtOffset(Position, 0) > 0)
                        {
                            BlocksToKeep.Add(Position, AllNodes[i].Attributes.GetNamedItem("name").Value);
                        }
                        Position += 20;
                        break;
                    case "_field_array":
                    case "_field_struct": 
                        break;
                    case "_field_explanation":
                    case "_field_custom":
                    case "_field_comment":
                        break;
                    case "_field_reference_v2":
                    case "_field_reference_64":
                        Position += 28;
                        break;
                    case "_field_angle_bounds":
                    case "_field_fraction_bounds":
                    case "_field_real_bounds":
                        Position += 8;
                        break;
                    case "_field_real_point_2d":
                        Position += 8;
                        break;
                    case "_field_real_point_3d":
                        Position += 12;
                        break;
                    case "_field_real_vector_2d":
                        Position += 8;
                        break;
                    case "_field_real_vector_3d":
                        Position += 12;
                        break;
                    case "_field_real_quaternion":
                        Position += 16;
                        break;
                    case "_field_real_plane_2d":
                        Position += 8;
                        break;
                    case "_field_real_plane_3d":
                        Position += 12;
                        break;
                    case "_field_real_euler_angles_2d":
                        Position += 8;
                        break;
                    case "_field_real_euler_angles_3d":
                        Position += 12;
                        break;
                    case "_field_rgb_color":
                        Position += 12;
                        break;
                    case "_field_real_rgb_color":
                        Position += 16;
                        break;
                    case "_field_real_argb_color":
                        Position += 16;
                        break;
                    case "_field_real":
                    case "_field_real_fraction":
                    case "_field_angle":
                        Position += 4;
                        break;
                    case "_field_int64_integer":
                    case "_field_qword_integer":
                        Position += 8;
                        break;
                    case "_field_long_integer":
                    case "_field_dword_integer":
                    case "_field_long_block_index":
                        Position += 4;
                        break;
                    case "_field_string_id":
                        Position += 4;
                        break;
                    case "_field_short_integer":
                    case "_field_word_integer":
                    case "_field_short_block_index":
                    case "_field_custom_short_block_index":
                        Position += 2;
                        break;
                    case "_field_char_integer":
                    case "_field_byte_integer":
                    case "_field_char_block_index":
                        Position += 1;
                        break;
                    case "_field_point_2d":
                        Position += 4;
                        break;
                    case "_field_long_enum":
                        Position += 4;
                        break;
                    case "_field_short_enum":
                        Position += 2;
                        break;
                    case "_field_char_enum":
                        Position += 1;
                        break;
                    case "_field_long_flags":
                    case "_field_long_block_flags":
                        Position += 4;
                        break;
                    case "_field_short_flags":
                    case "_field_word_flags":
                        Position += 2;
                        break;
                    case "_field_char_flags":
                    case "_field_byte_flags":
                        Position += 1;
                        break;
                    case "_field_data_64":
                    case "_field_data_v2":
                        Position += 24;
                        break;
                    case "_field_long_string":
                        Position += 256;
                        break;
                    case "_field_string":
                        Position += 32;
                        break;
                    default:
                        System.Diagnostics.Debug.WriteLine("Found unexpected NodeItem: {0}", AllNodes[i].Name);
                        System.Diagnostics.Debug.WriteLine("Offsets may be incorrect");
                        break;
                }
            }
            
            PluginXml.Load(PluginPath);
            AllNodes = PluginXml.SelectNodes("//*");
            for (int i = 0; i < AllNodes.Count; i++)
            {
                switch (AllNodes[i].Name.ToLower())
                {
                    case "_field_block_64":
                    case "_field_block_v2":
                        if (BlocksToKeep.ContainsValue(AllNodes[i].Attributes.GetNamedItem("name").Value))
                        {
                            int BlockOffset = 0;
                            foreach (KeyValuePair<int, string> ValuePair in BlocksToKeep)
                            {
                                if (ValuePair.Value == AllNodes[i].Attributes.GetNamedItem("name").Value)
                                {
                                    BlockOffset = ValuePair.Key;
                                    break;
                                }
                            }
                            for (int y = 0; y < Tag.TagBlockAtOffset(BlockOffset, 0); y++)
                            {
                                foreach (XmlNode Child in AllNodes[i].ChildNodes)
                                {
                                    XmlNode NewNode = NewXml.ImportNode(Child, true);
                                    NewXml.DocumentElement.AppendChild(NewNode);
                                }
                            }
                        }
                        break;
                    default:
                        break;
                }
            }
            NewXml.Save("tempPlugin.xml");
            PluginXml.Load("tempPlugin.xml");
            RemoveBlocks(PluginXml, Tag, Offset);
            PluginXml.Load("tempPlugin.xml");
            AllNodes = PluginXml.SelectNodes("//*");

            for (int i = 0; i < AllNodes.Count; i++)
            {
                switch (AllNodes[i].Name.ToLower()) //get item names for enums, flags
                {
                    case "plugin": //ignore, we know it's a plugin
                    case "item": //do something else for this you lazy bum
                        break;
                    case "_field_pad":
                    case "_field_skip":
                        PluginItems.Add(new PluginItem { Name = AllNodes[i].Attributes.GetNamedItem("name").Value, FieldType = PluginField.Padding, Offset = Position });
                        Position += int.Parse(AllNodes[i].Attributes.GetNamedItem("length").Value);
                        break;
                    case "_field_block_64":
                    case "_field_block_v2":
                        PluginItems.Add(new PluginItem { Name = AllNodes[i].Attributes.GetNamedItem("name").Value, FieldType = PluginField.TagBlock, Offset = Position });
                        Position += 20;
                        break;
                    case "_field_array":
                    case "_field_struct":
                        PluginItems.Add(new PluginItem { Name = AllNodes[i].Attributes.GetNamedItem("name").Value, FieldType = PluginField.TagStruct, Offset = Position });
                        break;
                    case "_field_explanation":
                    case "_field_custom":
                    case "_field_comment":
                        PluginItems.Add(new PluginItem { Name = AllNodes[i].Attributes.GetNamedItem("name").Value, FieldType = PluginField.Comment, Offset = Position });
                        break;
                    case "_field_reference_v2":
                    case "_field_reference_64":
                        PluginItems.Add(new PluginItem { Name = AllNodes[i].Attributes.GetNamedItem("name").Value, FieldType = PluginField.TagReference, Offset = Position });
                        Position += 28;
                        break;
                    case "_field_angle_bounds":
                    case "_field_fraction_bounds":
                    case "_field_real_bounds":
                        PluginItems.Add(new PluginItem { Name = AllNodes[i].Attributes.GetNamedItem("name").Value, FieldType = PluginField.RealBounds, Offset = Position });
                        Position += 8;
                        break;
                    case "_field_real_point_2d":
                        PluginItems.Add(new PluginItem { Name = AllNodes[i].Attributes.GetNamedItem("name").Value, FieldType = PluginField.RealPoint2D, Offset = Position });
                        Position += 8;
                        break;
                    case "_field_real_point_3d":
                        PluginItems.Add(new PluginItem { Name = AllNodes[i].Attributes.GetNamedItem("name").Value, FieldType = PluginField.RealPoint3D, Offset = Position });
                        Position += 12;
                        break;
                    case "_field_real_vector_2d":
                        PluginItems.Add(new PluginItem { Name = AllNodes[i].Attributes.GetNamedItem("name").Value, FieldType = PluginField.Vector2D, Offset = Position });
                        Position += 8;
                        break;
                    case "_field_real_vector_3d":
                        PluginItems.Add(new PluginItem { Name = AllNodes[i].Attributes.GetNamedItem("name").Value, FieldType = PluginField.Vector3D, Offset = Position });
                        Position += 12;
                        break;
                    case "_field_real_quaternion": //i am in hell and this is the devil
                        PluginItems.Add(new PluginItem { Name = AllNodes[i].Attributes.GetNamedItem("name").Value, FieldType = PluginField.Quaternion, Offset = Position });
                        Position += 16;
                        break;
                    case "_field_real_plane_2d":
                        PluginItems.Add(new PluginItem { Name = AllNodes[i].Attributes.GetNamedItem("name").Value, FieldType = PluginField.Plane2D, Offset = Position });
                        Position += 8;
                        break;
                    case "_field_real_plane_3d":
                        PluginItems.Add(new PluginItem { Name = AllNodes[i].Attributes.GetNamedItem("name").Value, FieldType = PluginField.Plane3D, Offset = Position });
                        Position += 16;
                        break;
                    case "_field_real_euler_angles_2d":
                        PluginItems.Add(new PluginItem { Name = AllNodes[i].Attributes.GetNamedItem("name").Value, FieldType = PluginField.EulerAngle2D, Offset = Position });
                        Position += 8;
                        break;
                    case "_field_real_euler_angles_3d":
                        PluginItems.Add(new PluginItem { Name = AllNodes[i].Attributes.GetNamedItem("name").Value, FieldType = PluginField.EulerAngle3D, Offset = Position });
                        Position += 12;
                        break;
                    case "_field_rgb_color":
                        PluginItems.Add(new PluginItem { Name = AllNodes[i].Attributes.GetNamedItem("name").Value, FieldType = PluginField.RGBColor, Offset = Position });
                        Position += 4;
                        break;
                    case "_field_real_rgb_color":
                        PluginItems.Add(new PluginItem { Name = AllNodes[i].Attributes.GetNamedItem("name").Value, FieldType = PluginField.RealRGBColor, Offset = Position });
                        Position += 12;
                        break;
                    case "_field_real_argb_color":
                        PluginItems.Add(new PluginItem { Name = AllNodes[i].Attributes.GetNamedItem("name").Value, FieldType = PluginField.RealARGBColor, Offset = Position });
                        Position += 16;
                        break;
                    case "_field_real":
                    case "_field_real_fraction":
                    case "_field_angle": //do something for this
                        PluginItems.Add(new PluginItem { Name = AllNodes[i].Attributes.GetNamedItem("name").Value, FieldType = PluginField.Real, Offset = Position });
                        Position += 4;
                        break;
                    case "_field_short_integer_bounds":
                        PluginItems.Add(new PluginItem { Name = AllNodes[i].Attributes.GetNamedItem("name").Value, FieldType = PluginField.ShortBounds, Offset = Position });
                        Position += 4;
                        break;
                    case "_field_int64_integer":
                    case "_field_qword_integer":
                        PluginItems.Add(new PluginItem { Name = AllNodes[i].Attributes.GetNamedItem("name").Value, FieldType = PluginField.Int64, Offset = Position });
                        Position += 8;
                        break;
                    case "_field_long_integer":
                    case "_field_dword_integer":
                    case "_field_long_block_index":
                        PluginItems.Add(new PluginItem { Name = AllNodes[i].Attributes.GetNamedItem("name").Value, FieldType = PluginField.Int32, Offset = Position });
                        Position += 4;
                        break;
                    case "_field_string_id":
                        PluginItems.Add(new PluginItem { Name = AllNodes[i].Attributes.GetNamedItem("name").Value, FieldType = PluginField.StringID, Offset = Position });
                        Position += 4;
                        break;
                    case "_field_short_integer":
                    case "_field_word_integer":
                    case "_field_short_block_index":
                    case "_field_custom_short_block_index":
                        PluginItems.Add(new PluginItem { Name = AllNodes[i].Attributes.GetNamedItem("name").Value, FieldType = PluginField.Int16, Offset = Position });
                        Position += 2;
                        break;
                    case "_field_char_integer":
                    case "_field_byte_integer":
                    case "_field_char_block_index":
                        PluginItems.Add(new PluginItem { Name = AllNodes[i].Attributes.GetNamedItem("name").Value, FieldType = PluginField.Int8, Offset = Position });
                        Position += 1;
                        break;
                    case "_field_point_2d":
                        PluginItems.Add(new PluginItem { Name = AllNodes[i].Attributes.GetNamedItem("name").Value, FieldType = PluginField.Point2D, Offset = Position });
                        Position += 4;
                        break;
                    case "_field_long_enum":
                        PluginItems.Add(new PluginItem { Name = AllNodes[i].Attributes.GetNamedItem("name").Value, FieldType = PluginField.Enum32, Offset = Position });
                        Position += 4;
                        break;
                    case "_field_short_enum":
                        PluginItems.Add(new PluginItem { Name = AllNodes[i].Attributes.GetNamedItem("name").Value, FieldType = PluginField.Enum16, Offset = Position });
                        Position += 2;
                        break;
                    case "_field_char_enum":
                        PluginItems.Add(new PluginItem { Name = AllNodes[i].Attributes.GetNamedItem("name").Value, FieldType = PluginField.Enum8, Offset = Position });
                        Position += 1;
                        break;
                    case "_field_long_flags":
                    case "_field_long_block_flags":
                        PluginItems.Add(new PluginItem { Name = AllNodes[i].Attributes.GetNamedItem("name").Value, FieldType = PluginField.Flags32, Offset = Position });
                        Position += 4;
                        break;
                    case "_field_short_flags":
                    case "_field_word_flags":
                        PluginItems.Add(new PluginItem { Name = AllNodes[i].Attributes.GetNamedItem("name").Value, FieldType = PluginField.Flags16, Offset = Position });
                        Position += 2;
                        break;
                    case "_field_char_flags":
                    case "_field_byte_flags":
                        PluginItems.Add(new PluginItem { Name = AllNodes[i].Attributes.GetNamedItem("name").Value, FieldType = PluginField.Flags8, Offset = Position });
                        Position += 1;
                        break;
                    case "_field_data_64":
                    case "_field_data_v2":
                        PluginItems.Add(new PluginItem { Name = AllNodes[i].Attributes.GetNamedItem("name").Value, FieldType = PluginField.DataReference, Offset = Position });
                        Position += 24;
                        break;
                    case "_field_long_string":
                        PluginItems.Add(new PluginItem { Name = AllNodes[i].Attributes.GetNamedItem("name").Value, FieldType = PluginField.LongString, Offset = Position });
                        Position += 256;
                        break;
                    case "_field_string":
                        PluginItems.Add(new PluginItem { Name = AllNodes[i].Attributes.GetNamedItem("name").Value, FieldType = PluginField.String, Offset = Position });
                        Position += 32;
                        break;
                    default:
                        System.Diagnostics.Debug.WriteLine("Found unexpected NodeItem: {0}", AllNodes[i].Name);
                        System.Diagnostics.Debug.WriteLine("Offsets may be incorrect");
                        break;
                }
            }
            File.Delete("tempPlugin.xml");
            return PluginItems;
        }

        public List<PluginItem> LoadGenericTag(Tag Tag, int Offset)
        {
            List<PluginItem> PluginItems = new List<PluginItem>();
            int Position = Offset;

            for (int i = 0; i < Tag.Header.DataSize; i += 4)
            {
                PluginItems.Add(new PluginItem { Name = "Unknown Field " + i, FieldType = PluginField.Int32, Offset = Position });
                Position += 4;
            }

            return PluginItems;
        }


        public List<PluginItem> LoadPlugin(string PluginPath, Tag Tag, int Offset)
        {
            List<PluginItem> PluginItems = new List<PluginItem>();
            XmlDocument PluginXml = new XmlDocument();
            PluginXml.Load(PluginPath);
            PluginXml.Save("tempPlugin.xml");
            RemoveBlocks(PluginXml, Tag, Offset);
            
            PluginXml.Load("tempPlugin.xml");
            XmlNodeList AllNodes = PluginXml.SelectNodes("//*");

            int Position = Offset;

            for (int i = 0; i < AllNodes.Count; i++)
            {
                switch (AllNodes[i].Name.ToLower()) //get item names for enums, flags
                {
                    case "plugin": //ignore, we know it's a plugin
                    case "item": //do something else for this you lazy bum
                        break;
                    case "_field_pad":
                    case "_field_skip":
                        PluginItems.Add(new PluginItem { Name = AllNodes[i].Attributes.GetNamedItem("name").Value, FieldType = PluginField.Padding, Offset = Position });
                        Position += int.Parse(AllNodes[i].Attributes.GetNamedItem("length").Value);
                        break;
                    case "_field_block_64":
                    case "_field_block_v2":
                        PluginItems.Add(new PluginItem { Name = AllNodes[i].Attributes.GetNamedItem("name").Value, FieldType = PluginField.TagBlock, Offset = Position });
                        Position += 20;
                        break;
                    case "_field_array":
                    case "_field_struct":
                        PluginItems.Add(new PluginItem { Name = AllNodes[i].Attributes.GetNamedItem("name").Value, FieldType = PluginField.TagStruct, Offset = Position });
                        break;
                    case "_field_explanation":
                    case "_field_custom":
                    case "_field_comment":
                        PluginItems.Add(new PluginItem { Name = AllNodes[i].Attributes.GetNamedItem("name").Value, FieldType = PluginField.Comment, Offset = Position });
                        break;
                    case "_field_reference_v2":
                    case "_field_reference_64":
                        PluginItems.Add(new PluginItem { Name = AllNodes[i].Attributes.GetNamedItem("name").Value, FieldType = PluginField.TagReference, Offset = Position });
                        Position += 28;
                        break;
                    case "_field_angle_bounds":
                    case "_field_fraction_bounds":
                    case "_field_real_bounds":
                        PluginItems.Add(new PluginItem { Name = AllNodes[i].Attributes.GetNamedItem("name").Value, FieldType = PluginField.RealBounds, Offset = Position });
                        Position += 8;
                        break;
                    case "_field_real_point_2d":
                        PluginItems.Add(new PluginItem { Name = AllNodes[i].Attributes.GetNamedItem("name").Value, FieldType = PluginField.RealPoint2D, Offset = Position });
                        Position += 8;
                        break;
                    case "_field_real_point_3d":
                        PluginItems.Add(new PluginItem { Name = AllNodes[i].Attributes.GetNamedItem("name").Value, FieldType = PluginField.RealPoint3D, Offset = Position });
                        Position += 12;
                        break;
                    case "_field_real_vector_2d":
                        PluginItems.Add(new PluginItem { Name = AllNodes[i].Attributes.GetNamedItem("name").Value, FieldType = PluginField.Vector2D, Offset = Position });
                        Position += 8;
                        break;
                    case "_field_real_vector_3d":
                        PluginItems.Add(new PluginItem { Name = AllNodes[i].Attributes.GetNamedItem("name").Value, FieldType = PluginField.Vector3D, Offset = Position });
                        Position += 12;
                        break;
                    case "_field_real_quaternion": //i am in hell and this is the devil
                        PluginItems.Add(new PluginItem { Name = AllNodes[i].Attributes.GetNamedItem("name").Value, FieldType = PluginField.Quaternion, Offset = Position });
                        Position += 16;
                        break;
                    case "_field_real_plane_2d":
                        PluginItems.Add(new PluginItem { Name = AllNodes[i].Attributes.GetNamedItem("name").Value, FieldType = PluginField.Plane2D, Offset = Position });
                        Position += 8;
                        break;
                    case "_field_real_plane_3d":
                        PluginItems.Add(new PluginItem { Name = AllNodes[i].Attributes.GetNamedItem("name").Value, FieldType = PluginField.Plane3D, Offset = Position });
                        Position += 12;
                        break;
                    case "_field_real_euler_angles_2d":
                        PluginItems.Add(new PluginItem { Name = AllNodes[i].Attributes.GetNamedItem("name").Value, FieldType = PluginField.EulerAngle2D, Offset = Position });
                        Position += 8;
                        break;
                    case "_field_real_euler_angles_3d":
                        PluginItems.Add(new PluginItem { Name = AllNodes[i].Attributes.GetNamedItem("name").Value, FieldType = PluginField.EulerAngle3D, Offset = Position });
                        Position += 12;
                        break;
                    case "_field_rgb_color":
                        PluginItems.Add(new PluginItem { Name = AllNodes[i].Attributes.GetNamedItem("name").Value, FieldType = PluginField.RGBColor, Offset = Position });
                        Position += 4;
                        break;
                    case "_field_real_rgb_color":
                        PluginItems.Add(new PluginItem { Name = AllNodes[i].Attributes.GetNamedItem("name").Value, FieldType = PluginField.RealRGBColor, Offset = Position });
                        Position += 12;
                        break;
                    case "_field_real_argb_color":
                        PluginItems.Add(new PluginItem { Name = AllNodes[i].Attributes.GetNamedItem("name").Value, FieldType = PluginField.RealARGBColor, Offset = Position });
                        Position += 16;
                        break;
                    case "_field_real":
                    case "_field_real_fraction":
                    case "_field_angle": //do something for this
                        PluginItems.Add(new PluginItem { Name = AllNodes[i].Attributes.GetNamedItem("name").Value, FieldType = PluginField.Real, Offset = Position });
                        Position += 4;
                        break;
                    case "_field_short_integer_bounds":
                        PluginItems.Add(new PluginItem { Name = AllNodes[i].Attributes.GetNamedItem("name").Value, FieldType = PluginField.ShortBounds, Offset = Position });
                        Position += 4;
                        break;
                    case "_field_int64_integer":
                    case "_field_qword_integer":
                        PluginItems.Add(new PluginItem { Name = AllNodes[i].Attributes.GetNamedItem("name").Value, FieldType = PluginField.Int64, Offset = Position });
                        Position += 8;
                        break;
                    case "_field_long_integer":
                    case "_field_dword_integer":
                    case "_field_long_block_index":
                        PluginItems.Add(new PluginItem { Name = AllNodes[i].Attributes.GetNamedItem("name").Value, FieldType = PluginField.Int32, Offset = Position });
                        Position += 4;
                        break;
                    case "_field_string_id":
                        PluginItems.Add(new PluginItem { Name = AllNodes[i].Attributes.GetNamedItem("name").Value, FieldType = PluginField.StringID, Offset = Position });
                        Position += 4;
                        break;
                    case "_field_short_integer":
                    case "_field_word_integer":
                    case "_field_short_block_index":
                    case "_field_custom_short_block_index":
                        PluginItems.Add(new PluginItem { Name = AllNodes[i].Attributes.GetNamedItem("name").Value, FieldType = PluginField.Int16, Offset = Position });
                        Position += 2;
                        break;
                    case "_field_char_integer":
                    case "_field_byte_integer":
                    case "_field_char_block_index":
                        PluginItems.Add(new PluginItem { Name = AllNodes[i].Attributes.GetNamedItem("name").Value, FieldType = PluginField.Int8, Offset = Position });
                        Position += 1;
                        break;
                    case "_field_point_2d":
                        PluginItems.Add(new PluginItem { Name = AllNodes[i].Attributes.GetNamedItem("name").Value, FieldType = PluginField.Point2D, Offset = Position });
                        Position += 4;
                        break;
                    case "_field_long_enum":
                        PluginItems.Add(new PluginItem { Name = AllNodes[i].Attributes.GetNamedItem("name").Value, FieldType = PluginField.Enum32, Offset = Position });
                        Position += 4;
                        break;
                    case "_field_short_enum":
                        PluginItems.Add(new PluginItem { Name = AllNodes[i].Attributes.GetNamedItem("name").Value, FieldType = PluginField.Enum16, Offset = Position });
                        Position += 2;
                        break;
                    case "_field_char_enum":
                        PluginItems.Add(new PluginItem { Name = AllNodes[i].Attributes.GetNamedItem("name").Value, FieldType = PluginField.Enum8, Offset = Position });
                        Position += 1;
                        break;
                    case "_field_long_flags":
                    case "_field_long_block_flags":
                        PluginItems.Add(new PluginItem { Name = AllNodes[i].Attributes.GetNamedItem("name").Value, FieldType = PluginField.Flags32, Offset = Position });
                        Position += 4;
                        break;
                    case "_field_short_flags":
                    case "_field_word_flags":
                        PluginItems.Add(new PluginItem { Name = AllNodes[i].Attributes.GetNamedItem("name").Value, FieldType = PluginField.Flags16, Offset = Position });
                        Position += 2;
                        break;
                    case "_field_char_flags":
                    case "_field_byte_flags":
                        PluginItems.Add(new PluginItem { Name = AllNodes[i].Attributes.GetNamedItem("name").Value, FieldType = PluginField.Flags8, Offset = Position });
                        Position += 1;
                        break;
                    case "_field_data_64":
                    case "_field_data_v2":
                        PluginItems.Add(new PluginItem { Name = AllNodes[i].Attributes.GetNamedItem("name").Value, FieldType = PluginField.DataReference, Offset = Position });
                        Position += 24;
                        break;
                    case "_field_long_string":
                        PluginItems.Add(new PluginItem { Name = AllNodes[i].Attributes.GetNamedItem("name").Value, FieldType = PluginField.LongString, Offset = Position });
                        Position += 256;
                        break;
                    case "_field_string":
                        PluginItems.Add(new PluginItem { Name = AllNodes[i].Attributes.GetNamedItem("name").Value, FieldType = PluginField.String, Offset = Position });
                        Position += 32;
                        break;
                    default:
                        System.Diagnostics.Debug.WriteLine("Found unexpected NodeItem: {0}", AllNodes[i].Name);
                        System.Diagnostics.Debug.WriteLine("Offsets may be incorrect");
                        break;
                }
            }

            File.Delete("tempPlugin.xml");
            

            /*
            StreamWriter sw = new StreamWriter("test.txt");
            foreach (PluginItem pluginItem in PluginItems)
            {
                sw.WriteLine("Item: {0} | Type: {1} | Offset: {2}", pluginItem.Name, pluginItem.FieldType, pluginItem.Offset);

            }
            sw.Close();
            */

            return PluginItems;
        }
    }

    public class PluginItem
    {
        public string Name { get; set; }
        public PluginField FieldType { get; set; }
        public int Offset { get; set; }
        public object Value { get; set; }
        private bool Modified { get; set; }

        public void SetModified()
        {
            Modified = true;
        }

        public bool GetModified()
        {
            return Modified;
        }

        public override string ToString()
        {
            return Name.PadRight(75) + " | Type: " + FieldType.ToString().PadRight(25) + " | Offset: " + Offset.ToString().PadRight(15) + " | Value: " + Value;
        }
    }

    public enum PluginField
    {
        Comment,
        Padding,
        Flags8,
        Flags16,
        Flags32,
        Flags64,
        Enum8,
        Enum16,
        Enum32,
        Enum64,
        Int8,
        Int16,
        Int32,
        Int64,
        Real,
        Double,
        ShortBounds,
        RealBounds,
        Point2D,
        Point3D,
        RealPoint2D,
        RealPoint3D,
        Vector2D,
        Vector3D,
        Quaternion,
        Plane2D,
        Plane3D,
        Rectangle2D,
        EulerAngle2D,
        EulerAngle3D,
        RGBColor,
        RealRGBColor,
        RealARGBColor,
        RealHSVColor,
        RealAHSVColor,
        StringID,
        String,
        LongString,
        DataReference,
        TagReference,
        TagBlock,
        TagStruct,
    }
}
