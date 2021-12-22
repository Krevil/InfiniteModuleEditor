using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using Microsoft.Win32;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.IO;
using System.Xml;
using System.Runtime.Serialization.Formatters.Binary;
using System.Runtime.InteropServices;
using OodleSharp;

namespace InfiniteModuleEditor
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        public Module Module;
        public FileStream ModuleStream;
        public bool FileStreamOpen = false;
        public ModuleFile ModuleFile;
        public bool TagOpen = false;
        public string TagFileName;
        public MemoryStream TagStream;

        //TODO: tag editing, UI style improvements, add a header so user knows what module + tag they are editing, confirmation box for closing module/tag, have tag values be their actual name from the module list

        public MainWindow()
        {
            InitializeComponent();
        }

        private void CommandBinding_Open_CanExecute(object sender, CanExecuteRoutedEventArgs e)
        {
            e.CanExecute = true;
        }

        private void CommandBinding_Open_Executed(object sender, ExecutedRoutedEventArgs e)
        {
            var ofd = new OpenFileDialog();
            if (ofd.ShowDialog() == true)
            {
                if (FileStreamOpen)
                {
                    if (TagOpen)
                    {
                        MessageBox.Show("You have a tag open - close it first before opening another module.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                        return;
                    }
                    //check to confirm close
                    MessageBoxResult Result = MessageBox.Show("A Module is already open. Close it and open a new one?", "Module Open", MessageBoxButton.YesNoCancel, MessageBoxImage.Error);
                    if (Result != MessageBoxResult.Yes)
                        ModuleStream.Close();
                    else
                        return;
                }
                ModuleStream = new FileStream(ofd.FileName, FileMode.Open);
                Module = ModuleEditor.ReadModule(ModuleStream);
                TagList.ItemsSource = Module.ModuleFiles.Keys;
                TagList.Visibility = Visibility.Visible;
                TagListFilter.Visibility = Visibility.Visible;
                Close_Module.IsEnabled = true;
                FileStreamOpen = true;
                //show tag list, make clickable
            }
        }

        private void CommandBinding_Exit_CanExecute(object sender, CanExecuteRoutedEventArgs e)
        {
            e.CanExecute = true;
        }

        private void CommandBinding_Exit_Executed(object sender, ExecutedRoutedEventArgs e)
        {
            Application.Current.Shutdown();
        }

        private void TagList_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (!TagOpen)
            {
                string TagName = e.AddedItems[0].ToString();
                TagNameText.Text = TagName;
                TagNameText.Visibility = Visibility.Visible;
                TagStream = ModuleEditor.SaveTag(Module, ModuleStream, TagName);
                Module.ModuleFiles.TryGetValue(Module.ModuleFiles.Keys.ToList().Find(x => x.Contains(TagName)), out ModuleFile);
                ModuleFile.Tag = ModuleEditor.ReadTag(TagStream, TagName.Substring(TagName.LastIndexOf("\\") + 1, TagName.Length - TagName.LastIndexOf("\\") - 2));
                TagViewer.ItemsSource = ModuleFile.Tag.TagValues;
                TagViewer.Visibility = Visibility.Visible;
                SaveButton.Visibility = Visibility.Visible;
                ExitButton.Visibility = Visibility.Visible;
                TagOpen = true;
            }
            else
            {
                MessageBox.Show("You already have a tag open. Close it before opening another.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void TagListFilter_KeyDown(object sender, KeyEventArgs e)
        {
            if (FileStreamOpen)
                TagList.ItemsSource = Module.ModuleFiles.Keys.ToList().FindAll(x => x.Contains(TagListFilter.Text) == true);
        }

        private void TagListFilter_GotFocus(object sender, RoutedEventArgs e)
        {
            if (TagListFilter.Text == "Filter Tags") 
                TagListFilter.Text = "";
        }

        private void TagListFilter_LostFocus(object sender, RoutedEventArgs e)
        {
            if (TagListFilter.Text == "")
                TagListFilter.Text = "Filter Tags";
        }

        private void TagViewer_CellEditEnding(object sender, DataGridCellEditEndingEventArgs e)
        {
            //MessageBox.Show((e.EditingElement as TextBox).Text.ToString()); //stupid code to get new text
            int Index = ModuleFile.Tag.TagValues.FindIndex(x => x.Offset == (e.Row.Item as PluginItem).Offset && x.Name == (e.Row.Item as PluginItem).Name);
            try
            {
                (e.Row.Item as PluginItem).SetModified();
                ModuleFile.Tag.TagValues[Index] = (PluginItem)e.Row.Item;
            }
            catch
            {
                MessageBox.Show("Couldn't parse input for " + TagFileName, "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void SaveButton_Click(object sender, RoutedEventArgs e)
        {
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
                            TagReference TagRef = (TagReference)Item.Value;
                            TagStream.Seek(8, SeekOrigin.Current);
                            TagStream.Write(BitConverter.GetBytes(Convert.ToInt32(TagRef.GlobalID)), 0, 4);
                            TagStream.Seek(8, SeekOrigin.Current);
                            TagStream.Write(BitConverter.GetBytes(Convert.ToInt32(TagRef.GroupTag)), 0, 4);
                            break;
                        case PluginField.DataReference:
                            DataReferenceField DataRef = (DataReferenceField)Item.Value;
                            TagStream.Seek(20, SeekOrigin.Current);
                            TagStream.Write(BitConverter.GetBytes(Convert.ToInt32(DataRef.Size)), 0, 4);
                            break;
                        case PluginField.RealBounds:
                            RealBounds Bounds = (RealBounds)Item.Value;
                            TagStream.Write(BitConverter.GetBytes(Convert.ToSingle(Bounds.MinBound)), 0, 4);
                            TagStream.Write(BitConverter.GetBytes(Convert.ToSingle(Bounds.MaxBound)), 0, 4);
                            break;
                        case PluginField.Vector3D:
                            RealVector3D Vector = (RealVector3D)Item.Value;
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
            byte[] ModifiedTag = new byte[ModuleFile.Tag.Header.DataSize];
            TagStream.Seek(ModuleFile.Tag.Header.HeaderSize, SeekOrigin.Begin);
            TagStream.Read(ModifiedTag, 0, (int)ModuleFile.Tag.Header.DataSize);

            ModuleStream.Seek(ModuleFile.Blocks[1].ModuleOffset, SeekOrigin.Begin);
            byte[] CompressedModifiedTag = Oodle.Compress(ModifiedTag, ModifiedTag.Length, OodleFormat.Kraken, OodleCompressionLevel.Optimal5); //Set to optimal because a smaller file can be put back in but a bigger one is no bueno
            if (CompressedModifiedTag.Length <= ModuleFile.Blocks[1].BlockData.CompressedSize)
            {
                ModuleStream.Write(CompressedModifiedTag, 0, CompressedModifiedTag.Length);
                MessageBox.Show("Done!");
            }
            else
            {
                MessageBox.Show("Compression failed - Could not compress to or below desired size: " + ModuleFile.Blocks[1].BlockData.CompressedSize + ", the size it got was " + CompressedModifiedTag.Length);
            }
            //save compressed block from moduleeditor method
        }

        private void ExitButton_Click(object sender, RoutedEventArgs e)
        {
            TagStream.Close();
            SaveButton.Visibility = Visibility.Hidden;
            ExitButton.Visibility = Visibility.Hidden;
            TagViewer.Visibility = Visibility.Hidden;
            TagNameText.Visibility = Visibility.Hidden;
            TagOpen = false;
            
        }

        private void Close_Module_Click(object sender, RoutedEventArgs e)
        {
            if (FileStreamOpen)
            {
                if (!TagOpen)
                {
                    ModuleStream.Close();
                    FileStreamOpen = false;
                    TagList.Visibility = Visibility.Hidden;
                    TagListFilter.Visibility = Visibility.Hidden;
                }
                else
                    MessageBox.Show("You have a tag open - close it first before closing the module.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }
}
