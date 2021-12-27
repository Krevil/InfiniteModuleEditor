using System;
using System.IO;
using System.Text;
using System.Xml;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Runtime.Serialization.Formatters.Binary;
using System.Runtime.InteropServices;
using OodleSharp;


namespace InfiniteModuleEditor
{
    public class PluginReader
    {
        /// <summary>
        /// Loops through a nodes parents looking to see if one of them is a field block. If it is, we don't advance the position as this field isn't in the "main struct"
        /// </summary>
        /// <param name="Node">The node to begin looping through</param>
        /// <param name="Position">The position of the stream for the PluginItem</param>
        /// <param name="Size">The amount the position should be advanced</param>
        /// <returns>The new position after advancing, if applicable</returns>
        public int AdvancePosition(XmlNode Node, int Position, int Size, bool Recursive)
        {
            XmlNode TempNode = Node;
            if (Recursive == true)
            {
                while (TempNode.ParentNode.Name != "plugin")
                {
                    if (TempNode.ParentNode.Name == "_field_block_64" || TempNode.ParentNode.Name == "_field_block_v2")
                    {
                        return Position;
                    }
                    TempNode = TempNode.ParentNode;
                }
            }
            else if (TempNode.ParentNode.Name == "_field_block_64" || TempNode.ParentNode.Name == "_field_block_v2")
            {
                return Position;
            }
            return Position + Size;
        }

        public int AdvancePosition(XmlNode Node, int Position, int Size, string TargetParent)
        {
            XmlNode TempNode = Node;
            while (TempNode.ParentNode.Attributes.GetNamedItem("name").Value != TargetParent)
            {
                if (TempNode.ParentNode.Name == "_field_block_64" || TempNode.ParentNode.Name == "_field_block_v2")
                {
                    return Position;
                }
                TempNode = TempNode.ParentNode;
            }
            return Position + Size;
        }

        public bool AreAnyNodeAncestorsBlocks(XmlNode Node)
        {
            XmlNode TempNode = Node;
            while (TempNode.ParentNode.Name != "plugin")
            {
                if (TempNode.ParentNode.Name == "_field_block_64" || TempNode.ParentNode.Name == "_field_block_v2")
                {
                    return true;
                }
                TempNode = TempNode.ParentNode;
            }
            return false;
        }

        /// <summary>
        /// Generates a new PluginItem and adds it to the list based on the field type of a given node and returns the new position for the next PluginItem to be added at
        /// </summary>
        /// <param name="Node">The node to be inspected</param>
        /// <param name="PluginItems">The list of PluginItems to be added to</param>
        /// <param name="PluginBlocks">A list of PluginBlocks so their child fields can be parsed later</param>
        /// <param name="Tag">The tag currently loaded</param>
        /// <param name="Position">The position the stream should be at when reading the tag</param>
        /// <param name="OriginPosition">The original position of the stream (where this should subtract when parsing block positions)</param>
        /// <returns>The new position that the stream should be at</returns>
        public int AddPluginItems(XmlNode Node, List<PluginItem> PluginItems, int Position)
        {
            int OldPosition = Position;
            switch (Node.Name.ToLower()) //get item names for enums, flags
            {
                case "plugin": //ignore, we know it's a plugin
                case "item": //do something else for this you lazy bum
                    break;
                case "_field_pad":
                case "_field_skip":
                    Position = AdvancePosition(Node, Position, int.Parse(Node.Attributes.GetNamedItem("length").Value), true);
                    if (OldPosition != Position)
                        PluginItems.Add(new PluginItem { Name = Node.Attributes.GetNamedItem("name").Value, FieldType = PluginField.Padding, Offset = OldPosition });
                    break;
                case "_field_block_64":
                case "_field_block_v2":
                    Position = AdvancePosition(Node, Position, 20, true);
                    if (OldPosition != Position)
                        PluginItems.Add(new PluginItem { Name = Node.Attributes.GetNamedItem("name").Value, FieldType = PluginField.TagBlock, Offset = OldPosition });
                    break;
                case "_field_array":
                case "_field_struct":
                    PluginItems.Add(new PluginItem { Name = Node.Attributes.GetNamedItem("name").Value, FieldType = PluginField.TagStruct, Offset = OldPosition });
                    break;
                case "_field_explanation":
                case "_field_custom":
                case "_field_comment":
                    if (!AreAnyNodeAncestorsBlocks(Node))
                        PluginItems.Add(new PluginItem { Name = Node.Attributes.GetNamedItem("name").Value, FieldType = PluginField.Comment, Offset = OldPosition });
                    break;
                case "_field_reference_v2":
                case "_field_reference_64":
                    Position = AdvancePosition(Node, Position, 28, true);
                    if (OldPosition != Position)
                        PluginItems.Add(new PluginItem { Name = Node.Attributes.GetNamedItem("name").Value, FieldType = PluginField.TagReference, Offset = OldPosition });
                    break;
                case "_field_angle_bounds":
                case "_field_fraction_bounds":
                case "_field_real_bounds":
                    Position = AdvancePosition(Node, Position, 8, true);
                    if (OldPosition != Position)
                        PluginItems.Add(new PluginItem { Name = Node.Attributes.GetNamedItem("name").Value, FieldType = PluginField.RealBounds, Offset = OldPosition });
                    break;
                case "_field_real_point_2d":
                    Position = AdvancePosition(Node, Position, 8, true);
                    if (OldPosition != Position)
                        PluginItems.Add(new PluginItem { Name = Node.Attributes.GetNamedItem("name").Value, FieldType = PluginField.RealPoint2D, Offset = OldPosition });
                    break;
                case "_field_real_point_3d":
                    Position = AdvancePosition(Node, Position, 12, true);
                    if (OldPosition != Position)
                            PluginItems.Add(new PluginItem { Name = Node.Attributes.GetNamedItem("name").Value, FieldType = PluginField.RealPoint3D, Offset = OldPosition });
                    break;
                case "_field_real_vector_2d":
                    Position = AdvancePosition(Node, Position, 8, true);
                    if (OldPosition != Position)
                        PluginItems.Add(new PluginItem { Name = Node.Attributes.GetNamedItem("name").Value, FieldType = PluginField.Vector2D, Offset = OldPosition });
                    break;
                case "_field_real_vector_3d":
                    Position = AdvancePosition(Node, Position, 12, true);
                    if (OldPosition != Position)
                        PluginItems.Add(new PluginItem { Name = Node.Attributes.GetNamedItem("name").Value, FieldType = PluginField.Vector3D, Offset = OldPosition });
                    break;
                case "_field_real_quaternion": //i am in hell and this is the devil
                    Position = AdvancePosition(Node, Position, 16, true); 
                    if (OldPosition != Position)
                        PluginItems.Add(new PluginItem { Name = Node.Attributes.GetNamedItem("name").Value, FieldType = PluginField.Quaternion, Offset = OldPosition });
                    break;
                case "_field_real_plane_2d":
                    Position = AdvancePosition(Node, Position, 8, true); 
                    if (OldPosition != Position)
                        PluginItems.Add(new PluginItem { Name = Node.Attributes.GetNamedItem("name").Value, FieldType = PluginField.Plane2D, Offset = OldPosition });
                    break;
                case "_field_real_plane_3d":
                    Position = AdvancePosition(Node, Position, 12, true); 
                    if (OldPosition != Position)
                        PluginItems.Add(new PluginItem { Name = Node.Attributes.GetNamedItem("name").Value, FieldType = PluginField.Plane3D, Offset = OldPosition });
                    break;
                case "_field_real_euler_angles_2d":
                    Position = AdvancePosition(Node, Position, 8, true); 
                    if (OldPosition != Position)
                        PluginItems.Add(new PluginItem { Name = Node.Attributes.GetNamedItem("name").Value, FieldType = PluginField.EulerAngle2D, Offset = OldPosition });
                    break;
                case "_field_real_euler_angles_3d":
                    Position = AdvancePosition(Node, Position, 12, true); 
                    if (OldPosition != Position)
                        PluginItems.Add(new PluginItem { Name = Node.Attributes.GetNamedItem("name").Value, FieldType = PluginField.EulerAngle3D, Offset = OldPosition });
                    break;
                case "_field_rgb_color":
                    Position = AdvancePosition(Node, Position, 4, true);
                    if (OldPosition != Position)
                        PluginItems.Add(new PluginItem { Name = Node.Attributes.GetNamedItem("name").Value, FieldType = PluginField.RGBColor, Offset = OldPosition });
                    break;
                case "_field_real_rgb_color":
                    Position = AdvancePosition(Node, Position, 12, true); 
                    if (OldPosition != Position)
                        PluginItems.Add(new PluginItem { Name = Node.Attributes.GetNamedItem("name").Value, FieldType = PluginField.RealRGBColor, Offset = OldPosition });
                    break;
                case "_field_real_argb_color":
                    Position = AdvancePosition(Node, Position, 16, true); 
                    if (OldPosition != Position)
                        PluginItems.Add(new PluginItem { Name = Node.Attributes.GetNamedItem("name").Value, FieldType = PluginField.RealARGBColor, Offset = OldPosition });
                    break;
                case "_field_real":
                case "_field_real_fraction":
                case "_field_angle": //do something for this
                    Position = AdvancePosition(Node, Position, 4, true); 
                    if (OldPosition != Position)
                        PluginItems.Add(new PluginItem { Name = Node.Attributes.GetNamedItem("name").Value, FieldType = PluginField.Real, Offset = OldPosition });
                    break;
                case "_field_short_integer_bounds":
                    Position = AdvancePosition(Node, Position, 4, true); 
                    if (OldPosition != Position)
                        PluginItems.Add(new PluginItem { Name = Node.Attributes.GetNamedItem("name").Value, FieldType = PluginField.ShortBounds, Offset = OldPosition });
                    break;
                case "_field_int64_integer":
                case "_field_qword_integer":
                    Position = AdvancePosition(Node, Position, 8, true); 
                    if (OldPosition != Position)
                        PluginItems.Add(new PluginItem { Name = Node.Attributes.GetNamedItem("name").Value, FieldType = PluginField.Int64, Offset = OldPosition });
                    break;
                case "_field_long_integer":
                case "_field_dword_integer":
                case "_field_long_block_index":
                    Position = AdvancePosition(Node, Position, 4, true); 
                    if (OldPosition != Position)
                        PluginItems.Add(new PluginItem { Name = Node.Attributes.GetNamedItem("name").Value, FieldType = PluginField.Int32, Offset = OldPosition });
                    break;
                case "_field_string_id":
                    Position = AdvancePosition(Node, Position, 4, true); 
                    if (OldPosition != Position)
                        PluginItems.Add(new PluginItem { Name = Node.Attributes.GetNamedItem("name").Value, FieldType = PluginField.StringID, Offset = OldPosition });
                    break;
                case "_field_short_integer":
                case "_field_word_integer":
                case "_field_short_block_index":
                case "_field_custom_short_block_index":
                    Position = AdvancePosition(Node, Position, 2, true); 
                    if (OldPosition != Position)
                        PluginItems.Add(new PluginItem { Name = Node.Attributes.GetNamedItem("name").Value, FieldType = PluginField.Int16, Offset = OldPosition });
                    break;
                case "_field_char_integer":
                case "_field_byte_integer":
                case "_field_char_block_index":
                    Position = AdvancePosition(Node, Position, 1, true);
                    if (OldPosition != Position)
                        PluginItems.Add(new PluginItem { Name = Node.Attributes.GetNamedItem("name").Value, FieldType = PluginField.Int8, Offset = OldPosition });
                    break;
                case "_field_point_2d":
                    Position = AdvancePosition(Node, Position, 4, true); 
                    if (OldPosition != Position)
                        PluginItems.Add(new PluginItem { Name = Node.Attributes.GetNamedItem("name").Value, FieldType = PluginField.Point2D, Offset = OldPosition });
                    break;
                case "_field_long_enum":
                    Position = AdvancePosition(Node, Position, 4, true); 
                    if (OldPosition != Position)
                        PluginItems.Add(new PluginItem { Name = Node.Attributes.GetNamedItem("name").Value, FieldType = PluginField.Enum32, Offset = OldPosition });
                    break;
                case "_field_short_enum":
                    Position = AdvancePosition(Node, Position, 2, true); 
                    if (OldPosition != Position)
                        PluginItems.Add(new PluginItem { Name = Node.Attributes.GetNamedItem("name").Value, FieldType = PluginField.Enum16, Offset = OldPosition });
                    break;
                case "_field_char_enum":
                    Position = AdvancePosition(Node, Position, 1, true); 
                    if (OldPosition != Position)
                        PluginItems.Add(new PluginItem { Name = Node.Attributes.GetNamedItem("name").Value, FieldType = PluginField.Enum8, Offset = OldPosition });
                    break;
                case "_field_long_flags":
                case "_field_long_block_flags":
                    Position = AdvancePosition(Node, Position, 4, true); 
                    if (OldPosition != Position)
                        PluginItems.Add(new PluginItem { Name = Node.Attributes.GetNamedItem("name").Value, FieldType = PluginField.Flags32, Offset = OldPosition });
                    break;
                case "_field_short_flags":
                case "_field_word_flags":
                    Position = AdvancePosition(Node, Position, 2, true);
                    if (OldPosition != Position)
                        PluginItems.Add(new PluginItem { Name = Node.Attributes.GetNamedItem("name").Value, FieldType = PluginField.Flags16, Offset = OldPosition });
                    break;
                case "_field_char_flags":
                case "_field_byte_flags":
                    Position = AdvancePosition(Node, Position, 1, true); 
                    if (OldPosition != Position)
                        PluginItems.Add(new PluginItem { Name = Node.Attributes.GetNamedItem("name").Value, FieldType = PluginField.Flags8, Offset = OldPosition });
                    break;
                case "_field_data_64":
                case "_field_data_v2":
                    Position = AdvancePosition(Node, Position, 24, true); 
                    if (OldPosition != Position)
                        PluginItems.Add(new PluginItem { Name = Node.Attributes.GetNamedItem("name").Value, FieldType = PluginField.DataReference, Offset = OldPosition });
                    break;
                case "_field_long_string":
                    Position = AdvancePosition(Node, Position, 256, true);
                    if (OldPosition != Position)
                        PluginItems.Add(new PluginItem { Name = Node.Attributes.GetNamedItem("name").Value, FieldType = PluginField.LongString, Offset = OldPosition });
                    break;
                case "_field_string":
                    Position = AdvancePosition(Node, Position, 32, true);
                    if (OldPosition != Position)
                        PluginItems.Add(new PluginItem { Name = Node.Attributes.GetNamedItem("name").Value, FieldType = PluginField.String, Offset = OldPosition });
                    break;
                default:
                    System.Diagnostics.Debug.WriteLine("Found unexpected NodeItem: {0}", Node.Name);
                    System.Diagnostics.Debug.WriteLine("Offsets may be incorrect");
                    break;
            }
            return Position;
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
            XmlNodeList AllNodes = PluginXml.SelectNodes("//*");

            int Position = Offset;

            for (int i = 0; i < AllNodes.Count; i++)
            {
                Position = AddPluginItems(AllNodes[i], PluginItems, Position);
            }


            for (int y = 0; y < PluginItems.Count; y++)
            {
                if (PluginItems[y].FieldType == PluginField.TagBlock)
                {
                    int BlockCount = BitConverter.ToInt32(Tag.TagData, PluginItems[y].Offset + 16);
                    if (BlockCount > 0)
                    {
                        //System.Diagnostics.Debug.WriteLine("PI offset {0}", PI.Offset);
                        TagStruct TS = Tag.TagStructArray.First(x => x.FieldOffset == PluginItems[y].Offset);
                        DataBlock DB = Tag.DataBlockArray[TS.TargetIndex];
                        for (int i = 0; i < DB.Size; i += 4)
                        {
                            PluginItems.Add(new PluginItem { Name = "Unknown Field " + i, FieldType = PluginField.Int32, Offset = Offset + (int)DB.Offset + i});
                        }
                    }
                }
            }
            

            
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
