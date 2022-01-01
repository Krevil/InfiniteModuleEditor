using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace InfiniteModuleEditor.Controls
{
    /// <summary>
    /// Interaction logic for TagField.xaml
    /// </summary>
    public partial class TagField : UserControl
    {
        public TagField()
        {
            InitializeComponent();
            DataContext = this;
        }

        public string NameField
        {
            get
            {
                return FieldName.Text;
            }
            set
            {
                FieldName.Text = value;
            }
        }

        public string ValueField
        {
            get
            {
                return FieldValue.Text;
            }
            set
            {
                FieldValue.Text = value;
            }
        }

        public string TypeField
        {
            get
            {
                return FieldType.Text;
            }
            set
            {
                FieldType.Text = value;
            }
        }

        public string OffsetField
        {
            get
            {
                return FieldOffset.Text;
            }
            set
            {
                FieldOffset.Text = value;
            }
        }

        public int PluginItemIndex { get; set; }

        private void FieldValue_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (((TextBox)sender).IsFocused)
            {
                try
                {
                    int Index = MainWindow.ModuleFile.Tag.TagValues.FindIndex(x => x.Offset == int.Parse(FieldOffset.Text) && x.Name == FieldName.Text);
                    MainWindow.ModuleFile.Tag.TagValues[Index].Value = FieldValue.Text;
                    MainWindow.ModuleFile.Tag.TagValues[Index].SetModified();
                }
                catch
                {
                    GenericMessageBox.Show("Couldn't parse input for " + FieldName.Text, "Error", MessageBoxButton.OK);
                }
            }
        }
    }
}
