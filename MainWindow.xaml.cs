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

        //TODO: UI style improvements, add a header so user knows what module they are editing, parse tag blocks or you won't be able to read anything with them

        //

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
                ModuleStream = new FileStream(ofd.FileName, FileMode.Open, FileAccess.ReadWrite, FileShare.ReadWrite);
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
            if (TagListFilter.IsFocused)
                return;

            if (!TagOpen && FileStreamOpen)
            {
                string TagName = e.AddedItems[0].ToString();
                TagNameText.Text = TagName;
                TagNameText.Visibility = Visibility.Visible;
                TagStream = ModuleEditor.GetTag(Module, ModuleStream, TagName);
                Module.ModuleFiles.TryGetValue(Module.ModuleFiles.Keys.ToList().Find(x => x.Contains(TagName)), out ModuleFile);
                ModuleFile.Tag = ModuleEditor.ReadTag(TagStream, TagName.Substring(TagName.LastIndexOf("\\") + 1, TagName.Length - TagName.LastIndexOf("\\") - 2), ModuleFile);
                TagViewer.ItemsSource = ModuleFile.Tag.TagValues;
                TagViewer.Visibility = Visibility.Visible;
                TagSearch.Visibility = Visibility.Visible;
                SaveButton.Visibility = Visibility.Visible;
                CloseButton.Visibility = Visibility.Visible;
                SaveAndCloseButton.Visibility = Visibility.Visible;
                TagOpen = true;
            }
            else
            {
                MessageBox.Show("You already have a tag open. Close it before opening another.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void TagListFilter_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (FileStreamOpen && TagListFilter.Text != "Filter Tags")
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
            bool Result = ModuleEditor.WriteTag(ModuleFile, TagStream, ModuleStream);
            //save compressed block from moduleeditor method

            if (Result)
                MessageBox.Show("Done!", "Success", MessageBoxButton.OK);
            else
                MessageBox.Show("Failed to compress tag to the right size", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            TagStream.Close();
            SaveButton.Visibility = Visibility.Hidden;
            CloseButton.Visibility = Visibility.Hidden;
            SaveAndCloseButton.Visibility = Visibility.Hidden;
            TagViewer.Visibility = Visibility.Hidden;
            TagSearch.Visibility = Visibility.Hidden;
            TagNameText.Visibility = Visibility.Hidden;
            TagOpen = false;
            
        }

        private void SaveAndCloseButton_Click(object sender, RoutedEventArgs e)
        {
            bool Result = ModuleEditor.WriteTag(ModuleFile, TagStream, ModuleStream);
            //save compressed block from moduleeditor method
            if (Result)
            {
                MessageBox.Show("Done!", "Success", MessageBoxButton.OK);
                TagStream.Close();
                SaveButton.Visibility = Visibility.Hidden;
                CloseButton.Visibility = Visibility.Hidden;
                SaveAndCloseButton.Visibility = Visibility.Hidden;
                TagViewer.Visibility = Visibility.Hidden;
                TagSearch.Visibility = Visibility.Hidden;
                TagNameText.Visibility = Visibility.Hidden;
                TagOpen = false;
            }
            else 
                MessageBox.Show("Failed to compress tag to the right size");
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
                    MessageBox.Show("Failed to compress tag to the right size", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void TagSearch_TextChanged(object sender, TextChangedEventArgs e)
        {
            TagViewer.ItemsSource = ModuleFile.Tag.TagValues.ToList().FindAll(x => x.Name.Contains(TagSearch.Text) == true);
        }
    }
}
