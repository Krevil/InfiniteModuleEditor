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

namespace InfiniteModuleEditor
{
    /// <summary>
    /// Interaction logic for GenericMessageBox.xaml
    /// Code from BendEg on stackoverflow
    /// Design heavily inspired by Assembly - like everything else in this project
    /// </summary>
    public partial class GenericMessageBox : Window
    {
        public GenericMessageBox()
        {
            InitializeComponent();
        }

        public MessageBoxResult Result;
        private MessageBoxButton buttons;

        public MessageBoxButton Buttons
        {
            get { return buttons; }
            set
            {
                buttons = value;
                switch (buttons)
                {
                    case MessageBoxButton.OK:
                        MessageBtnOkay.Visibility = Visibility.Visible;
                        break;
                    case MessageBoxButton.YesNo:
                        MessageBtnYes.Visibility = Visibility.Visible;
                        MessageBtnNo.Visibility = Visibility.Visible;
                        break;
                    default:
                        throw new NotImplementedException();
                }
            }
        }

        public static MessageBoxResult Show(string Message, string Title, MessageBoxButton Option)
        {
            GenericMessageBox MsgBox = new GenericMessageBox();
            MsgBox.MessageBtnOkay.Visibility = Visibility.Hidden;
            MsgBox.MessageBtnYes.Visibility = Visibility.Hidden;
            MsgBox.MessageBtnNo.Visibility = Visibility.Hidden;
            MsgBox.MessageBody.Text = Message;
            MsgBox.MessageTitle.Text = Title;
            MsgBox.Buttons = Option;

            MsgBox.ShowDialog();
            return MsgBox.Result;
        }

        private void MessageBtnOkay_Click(object sender, RoutedEventArgs e)
        {
            Result = MessageBoxResult.OK;
            Close();
        }

        private void MessageBtnYes_Click(object sender, RoutedEventArgs e)
        {
            Result = MessageBoxResult.Yes;
            Close();
        }

        private void MessageBtnNo_Click(object sender, RoutedEventArgs e)
        {
            Result = MessageBoxResult.No;
            Close();
        }

        private void Grid_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            DragMove();
        }
    }
}
