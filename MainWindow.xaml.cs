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
using InfiniteModuleEditor.Controls;
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
        public static ModuleFile ModuleFile;
        public bool TagOpen = false;
        public string TagFileName;
        public MemoryStream TagStream;
        public FileStream TagFileStream;
        public List<PluginItem> FilteredList;

        //TODO: UI style improvements, parse tag blocks better

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
                        GenericMessageBox.Show("You have a tag open - close it first before opening another module.", "Error", MessageBoxButton.OK);
                        return;
                    }
                    //check to confirm close
                    MessageBoxResult Result = GenericMessageBox.Show("A Module is already open. Close it and open a new one?", "Module Open", MessageBoxButton.YesNo);
                    if (Result != MessageBoxResult.Yes)
                        ModuleStream.Close();
                    else
                        return;
                }
                try
                {
                    StatusBar.Text = " Loading module...";
                    ModuleStream = new FileStream(ofd.FileName, FileMode.Open, FileAccess.ReadWrite, FileShare.ReadWrite);
                    Module = ModuleEditor.ReadModule(ModuleStream);
                    TagList.ItemsSource = Module.ModuleFiles.Keys;
                    TagList.Visibility = Visibility.Visible;
                    TagListFilter.Visibility = Visibility.Visible;
                    Close_Module.IsEnabled = true;
                    FileStreamOpen = true;
                    StatusBar.Text = " Ready...";
                }
                catch
                {
                    GenericMessageBox.Show("This file is open in another process", "Error", MessageBoxButton.OK);
                    StatusBar.Text = " Ready...";
                }
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
            if (TagList.SelectedItem == null)
                return;

            if (!TagOpen && FileStreamOpen)
            {
                StatusBar.Text = " Loading tag...";
                TagFileName = e.AddedItems[0].ToString();
                TagNameText.Text = TagFileName;
                TagNameText.Visibility = Visibility.Visible;
                TagStream = ModuleEditor.GetTag(Module, ModuleStream, TagFileName);
                Module.ModuleFiles.TryGetValue(Module.ModuleFiles.Keys.ToList().Find(x => x.Contains(TagFileName)), out ModuleFile);
                ModuleFile.Tag = ModuleEditor.ReadTag(TagStream, TagFileName.Substring(TagFileName.LastIndexOf("\\") + 1, TagFileName.Length - TagFileName.LastIndexOf("\\") - 2), ModuleFile);
                TagSearch.Text = "";
                for (int i = 0; i < ModuleFile.Tag.TagValues.Count; i++)
                {
                    TagViewer.RowDefinitions.Add(new RowDefinition());
                    TagField TF = new TagField
                    {
                        NameField = ModuleFile.Tag.TagValues[i].Name,
                        TypeField = ModuleFile.Tag.TagValues[i].FieldType.ToString(),
                        ValueField = Convert.ToString(ModuleFile.Tag.TagValues[i].Value),
                        OffsetField = ModuleFile.Tag.TagValues[i].Offset.ToString(),
                    };
                    Grid.SetRow(TF, i);
                    TagViewer.Children.Add(TF);
                }
                ShowTagViewer(true);
                TagOpen = true;
                StatusBar.Text = " Ready...";
            }
            else
            {
                GenericMessageBox.Show("You already have a tag open. Close it before opening another.", "Error", MessageBoxButton.OK);
                TagList.SelectedItem = null;
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
                GenericMessageBox.Show("Couldn't parse input for " + TagFileName, "Error", MessageBoxButton.OK);
            }
        }

        private void SaveButton_Click(object sender, RoutedEventArgs e)
        {
            StatusBar.Text = " Saving...";
            bool Result = ModuleEditor.WriteTag(ModuleFile, TagStream, ModuleStream);
            if (Result)
                GenericMessageBox.Show("Done!", "Success", MessageBoxButton.OK);
            else
                GenericMessageBox.Show("Failed to compress tag to the right size", "Error", MessageBoxButton.OK);
            StatusBar.Text = " Ready...";
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            TagStream.Close();
            HideTagViewer();
            TagOpen = false;
            TagList.SelectedItem = null;
        }

        private void SaveAndCloseButton_Click(object sender, RoutedEventArgs e)
        {
            StatusBar.Text = " Saving...";
            bool Result = ModuleEditor.WriteTag(ModuleFile, TagStream, ModuleStream);
            if (Result)
            {
                GenericMessageBox.Show("Done!", "Success", MessageBoxButton.OK);
                TagStream.Close();
                HideTagViewer();
                TagOpen = false;
                TagList.SelectedItem = null;
            }
            else 
                GenericMessageBox.Show("Failed to compress tag to the right size", "Error", MessageBoxButton.OK);

            StatusBar.Text = " Ready...";
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
                {
                    TagStream.Close();
                    HideTagViewer();
                    TagOpen = false;
                    TagList.SelectedItem = null;
                    ModuleStream.Close();
                    FileStreamOpen = false;
                    TagList.Visibility = Visibility.Hidden;
                    TagListFilter.Visibility = Visibility.Hidden;
                }
            }
        }

        private void TagSearch_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (TagViewer != null)
            {
                if (TagSearch.Text != "" && TagSearch.Text != "Filter Tag Fields")
                {
                    FilteredList = ModuleFile.Tag.TagValues.FindAll(x => x.Name.ToLower().Contains(TagSearch.Text) == true);
                    foreach (TagField Child in TagViewer.Children)
                    {
                        if (FilteredList.Exists(x => x.Offset == int.Parse(Child.OffsetField)))
                        {
                            Child.Visibility = Visibility.Visible;
                            continue;
                        }

                        Child.Visibility = Visibility.Collapsed;
                    }
                }
                else
                {
                    foreach (TagField Child in TagViewer.Children)
                    {
                        Child.Visibility = Visibility.Visible;
                    }
                    FilteredList = null;
                }
            }
        }

        private void Menu_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            DragMove();
        }

        private void Menu_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            WindowState = WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;
        }

        private void Maximize_Click(object sender, RoutedEventArgs e)
        {
            WindowState = WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;
        }

        private void ExitButton_Click(object sender, RoutedEventArgs e)
        {
            Application.Current.Shutdown();
        }

        private void MinimizeButton_Click(object sender, RoutedEventArgs e)
        {
            WindowState = WindowState.Minimized;
        }

        private void ExtractTagButton_Click(object sender, RoutedEventArgs e)
        {
            var sfd = new SaveFileDialog
            {
                FileName = TagFileName.Substring(TagFileName.LastIndexOf("\\") + 1, TagFileName.Length - TagFileName.LastIndexOf("\\") - 2)
            };
            if (sfd.ShowDialog() == true)
            {
                StatusBar.Text = " Saving tag...";
                FileStream OutputStream = new FileStream(sfd.FileName, FileMode.Create);
                TagStream.Seek(0, SeekOrigin.Begin);
                TagStream.CopyTo(OutputStream);
                OutputStream.Close();
                StatusBar.Text = " Ready...";
            }
        }

        private void Open_Tag_Click(object sender, RoutedEventArgs e)
        {
            if (!TagOpen)
            {
                var ofd = new OpenFileDialog();
                if (ofd.ShowDialog() == true)
                {
                    TagFileStream = new FileStream(ofd.FileName, FileMode.Open);
                    TagNameText.Text = ofd.FileName;
                    TagNameText.Visibility = Visibility.Visible;
                    ModuleFile = new ModuleFile();
                    ModuleFile.Tag = ModuleEditor.ReadTag(TagFileStream, ofd.SafeFileName);
                    TagSearch.Text = "";
                    for (int i = 0; i < ModuleFile.Tag.TagValues.Count; i++)
                    {
                        TagViewer.RowDefinitions.Add(new RowDefinition());
                        TagField TF = new TagField
                        {
                            NameField = ModuleFile.Tag.TagValues[i].Name,
                            TypeField = ModuleFile.Tag.TagValues[i].FieldType.ToString(),
                            ValueField = Convert.ToString(ModuleFile.Tag.TagValues[i].Value),
                            OffsetField = ModuleFile.Tag.TagValues[i].Offset.ToString(),
                            PluginItemIndex = i
                        };
                        Grid.SetRow(TF, i);
                        TagViewer.Children.Add(TF);
                    }
                    ShowTagViewer(false);
                    TagOpen = true;
                    StatusBar.Text = " Ready...";
                }
            }
            else
            {
                GenericMessageBox.Show("You already have a tag open. Close it before opening another.", "Error", MessageBoxButton.OK);
                TagList.SelectedItem = null;
            }
        }

        private void FileSaveButton_Click(object sender, RoutedEventArgs e)
        {
            StatusBar.Text = " Saving...";
            bool Result = ModuleEditor.WriteTag(TagFileStream, ModuleFile.Tag);

            if (Result)
                GenericMessageBox.Show("Done!", "Success", MessageBoxButton.OK);
            else
                GenericMessageBox.Show("Unable to save file, it may be in use by another process", "Error", MessageBoxButton.OK);
            StatusBar.Text = " Ready...";
        }

        private void FileSaveAndCloseButton_Click(object sender, RoutedEventArgs e)
        {
            StatusBar.Text = " Saving...";
            bool Result = ModuleEditor.WriteTag(TagFileStream, ModuleFile.Tag);
            if (Result)
            {
                GenericMessageBox.Show("Done!", "Success", MessageBoxButton.OK);
                TagFileStream.Close();
                HideTagViewer();
                TagOpen = false;
                TagList.SelectedItem = null;
            }
            else
                GenericMessageBox.Show("Unable to save file, it may be in use by another process", "Error", MessageBoxButton.OK);

            StatusBar.Text = " Ready...";
        }

        private void FileCloseButton_Click(object sender, RoutedEventArgs e)
        {
            TagFileStream.Close();
            HideTagViewer();
            TagOpen = false;
            TagList.SelectedItem = null;
        }

        public void Window_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            if (WindowState == WindowState.Maximized)
            {
                BorderThickness = new Thickness(7);
            }
            else
            {
                BorderThickness = new Thickness(0);
            }
        }

        private void TagSearch_GotFocus(object sender, RoutedEventArgs e)
        {
            if (TagSearch.Text == "Filter Tags")
                TagSearch.Text = "";
        }

        private void TagSearch_LostFocus(object sender, RoutedEventArgs e)
        {
            if (TagSearch.Text == "")
                TagSearch.Text = "Filter Tag Fields";
        }

        private void FieldNameSorter_Click(object sender, RoutedEventArgs e)
        {
            
        }

        private void FieldValueSorter_Click(object sender, RoutedEventArgs e)
        {

        }

        private void FieldTypeSorter_Click(object sender, RoutedEventArgs e)
        {
            
        }

        private void FieldOffsetSorter_Click(object sender, RoutedEventArgs e)
        {

        }

        public void HideTagViewer()
        {
            FileSaveButton.Visibility = Visibility.Hidden;
            FileCloseButton.Visibility = Visibility.Hidden;
            FileSaveAndCloseButton.Visibility = Visibility.Hidden;
            SaveButton.Visibility = Visibility.Hidden;
            CloseButton.Visibility = Visibility.Hidden;
            SaveAndCloseButton.Visibility = Visibility.Hidden;
            ExtractTagButton.Visibility = Visibility.Hidden;
            TagViewer.Visibility = Visibility.Hidden;
            TagViewerScrollViewer.VerticalScrollBarVisibility = ScrollBarVisibility.Hidden;
            TagViewerScrollViewer.HorizontalScrollBarVisibility = ScrollBarVisibility.Hidden;
            TagViewerColumns.Visibility = Visibility.Hidden;
            TagSearch.Visibility = Visibility.Hidden;
            TagNameText.Visibility = Visibility.Hidden;
        }

        public void ShowTagViewer(bool TagFromModule)
        {
            TagViewer.Visibility = Visibility.Visible;
            TagViewerColumns.Visibility = Visibility.Visible;
            TagViewerScrollViewer.VerticalScrollBarVisibility = ScrollBarVisibility.Visible;
            TagViewerScrollViewer.HorizontalScrollBarVisibility = ScrollBarVisibility.Visible;
            TagSearch.Visibility = Visibility.Visible;
            if (TagFromModule)
            {
                SaveButton.Visibility = Visibility.Visible;
                CloseButton.Visibility = Visibility.Visible;
                SaveAndCloseButton.Visibility = Visibility.Visible;
                ExtractTagButton.Visibility = Visibility.Visible;
            }
            else
            {
                FileSaveButton.Visibility = Visibility.Visible;
                FileCloseButton.Visibility = Visibility.Visible;
                FileSaveAndCloseButton.Visibility = Visibility.Visible;
            }
        }
    }
}
