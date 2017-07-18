using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using DevExpress.Xpf.Core;
using DevExpress.Xpf.WindowsUI;
using DevExpress.Xpf.NavBar;
using DevExpress.Xpf.Bars;

namespace DXDC
{
    /// <summary>
    /// Interaction logic for GalleryView.xaml
    /// </summary>
    public partial class GalleryView : NavigationPage
    {
        public GalleryView()
        {
            InitializeComponent();
            InitializeCommand();
            InitializeNavBar();
        }

#region Initialize Commands
        private RoutedCommand cmdNavGeneral = new RoutedCommand();
        private void InitializeCommand()
        {
            CommandBinding cbNavGeneral = new CommandBinding(cmdNavGeneral, cbNavGeneral_Executed, (sender, e) => { e.CanExecute = true; e.Handled = true; });

            GVP.CommandBindings.AddRange(new CommandBinding[] { cbNavGeneral });
        }

        private void cbNavGeneral_Executed(object sender, ExecutedRoutedEventArgs e)
        {
            ProcessGalleryView(e.Parameter.ToString());
        }

        private void ProcessGalleryView(string v)
        {
            if (v == "allitems")
            {
                gallery_Home.Gallery.Groups[0].Items.Clear();
                gallery_Home.Gallery.Groups[0].Caption = $"ALL MOVIES:{XGlobal.CurrentContext.TotalMovies.Count}";

                XGlobal.CurrentContext.TotalMovies.ForEach(m =>
                {
                    GalleryItem gi = new GalleryItem();
                    gi.Caption = $"{m.ReleaseID}";
                    gi.Description = string.Join(", ",
                        XGlobal.CurrentContext.TotalStars
                        .Where(s => m.ActorUIDs.Contains(s.UniqueID))
                        .Select(s => s.JName).ToArray());
                    if ((gi.Description as string).Length > 10) gi.Description = (gi.Description as string).Substring(0, 10);
                    gi.Glyph = new BitmapImage(new Uri(m.CoverFileInfo.FullName));
                    gi.Tag = m;
                    gallery_Home.Gallery.Groups[0].Items.Add(gi);

                    //gi.Command = Command_SelectResult;
                    //gi.CommandTarget = list_ProcessInformation;
                    //gi.CommandParameter = gi;
                });
            }
        }
        #endregion

        private void InitializeNavBar()
        {
            navgroupGeneral.Items.Clear();
            NavBarItem _nbi_gen = new NavBarItem();
            //_nbi_cs.ImageSource = new BitmapImage(new Uri("pack://application:,,,/Images/private.png"));
            _nbi_gen.Content = "ALL Movies";
            _nbi_gen.Name = "allmovies";
            _nbi_gen.Command = cmdNavGeneral;
            _nbi_gen.CommandTarget = gallery_Home;
            _nbi_gen.CommandParameter = "allitems";
            navgroupGeneral.Items.Add(_nbi_gen);
        }
    }
}
