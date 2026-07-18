using System.Reflection;
using System.Windows;

namespace MojiCollaTool
{
    public partial class AboutWindow : Window
    {
        public AboutWindow()
        {
            InitializeComponent();
            VersionText.Text = $"バージョン {Assembly.GetExecutingAssembly().GetName().Version}";
            ForkNoticeText.Text = ProductIdentity.ForkNotice;
            ContactNoticeText.Text = ProductIdentity.ContactNotice;
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e) => Close();
    }
}
