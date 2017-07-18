using DevExpress.Xpf.Bars;
using DevExpress.Xpf.Core;
using DevExpress.Xpf.LayoutControl;
using DevExpress.Xpf.WindowsUI;
using HtmlAgilityPack;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Net;
using System.Runtime.Serialization.Formatters.Binary;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace DXDC
{
    /// <summary>
    /// Interaction logic for CreateNewDataView.xaml
    /// </summary>
    public partial class CreateNewDataView : NavigationPage
    {
        public MovieInfo CurrentMovieInfo { get; set; } = new MovieInfo();
        public bool IsCensored { get; set; } = true;
        public HtmlDocument CurrentHtmlDocument { get; set; } = new HtmlDocument();
        public ObservableCollection<StarInfo> CurrentStars { get; set; } = new ObservableCollection<StarInfo>();
        public DirectoryInfo CurrentStarLocation { get; set; }
        public string CurrentSearchKey { get; set; }
        ObservableCollection<MovieSample> CurrentMovieSamples = new ObservableCollection<MovieSample>();

        private DirectoryInfo CurrentSamplesLocation;
        private bool IsReadyProcess = false;

        public CreateNewDataView()
        {
            InitializeComponent();
            InitializeCommand();
        }

        public RoutedCommand Command_SelectResult = new RoutedCommand("SelectResult", typeof(MainWindow));
        public RoutedCommand Command_ProcessCurrentMovie = new RoutedCommand("ProcessCurrentMovie", typeof(MainWindow));

        private void InitializeCommand()
        {

            var OpenCmdBinding = new CommandBinding(ApplicationCommands.Open, OpenCmd_Executed, OpenCmd_CanExecute);
            var FindCmdBinding = new CommandBinding(ApplicationCommands.Find, FindCmd_Executed, FindCmd_CanExecute);
            CommandBinding cb_SelectResult = new CommandBinding(
                Command_SelectResult, 
                Command_SelectResult_Executed, 
                (sender, e) => { e.CanExecute = true; e.Handled = true; });
            CommandBinding cb_ProcessCurrentMovie = new CommandBinding(
                Command_ProcessCurrentMovie,
                Command_ProcessCurrentMovie_Executed,
                (sender, e) =>
                {
                    if (IsReadyProcess) e.CanExecute = true;
                    else e.CanExecute = false;
                    e.Handled = true;
                });

            this.CommandBindings.AddRange(new CommandBinding[] { OpenCmdBinding, FindCmdBinding, cb_SelectResult, cb_ProcessCurrentMovie });

            bn_Process.Command = Command_ProcessCurrentMovie;
        }

        private void InitializeUIControls()
        {
            gallery_SearchResult.Gallery.Groups[0].Items.Clear();
            group_SearchResult.State = GroupBoxState.Normal;
            group_Sample.State = GroupBoxState.Minimized;
            group_Cover.State = GroupBoxState.Normal;
            list_CurrentStars.ItemsSource = null;
            list_CurrentStars.Items.Clear();
            list_CurrentSamples.ItemsSource = null;
            list_CurrentSamples.Items.Clear();
            txt_ReleaseName.EditValue = null;
            txt_ReleaseID.EditValue = null;
            txt_ReleaseDate.EditValue = null;
            txt_StarNameJA.EditValue = null;
            txt_StarNameEN.EditValue = null;
            img_MovieCover.Source = null;
            img_MovieSample.Source = null;
            txt_ReleaseName.EditValue = null;
            list_ProcessInformation.Items.Clear();
        }

        private void Command_ProcessCurrentMovie_Executed(object sender, ExecutedRoutedEventArgs e)
        {
            IsReadyProcess = false;

            StarInfo TargetStar = list_CurrentStars.SelectedItem as StarInfo;
            var root_dc = CurrentMovieInfo.SourcePath.Root.CreateSubdirectory(XGlobal.ArchiveName);
            var root_si = TargetStar.LastLocation;
            var root_mi = root_si.CreateSubdirectory(CurrentMovieInfo.ReleaseID);
            //var dir_mcover = root_mi.CreateSubdirectory("-COVER-");
            var dir_msample = root_mi.CreateSubdirectory("-SAMPLE-");
            CurrentMovieInfo.MediaFiles.ForEach(m => m.LocalFileInfo.MoveTo(Path.Combine(root_mi.FullName, m.LocalFileInfo.Name)));
            CurrentMovieInfo.CoverFileInfo.MoveTo(Path.Combine(root_mi.FullName, CurrentMovieInfo.CoverFileInfo.Name));
            List<Uri> samples_newuri = new List<Uri>();
            //release sampleview
            img_MovieSample.Clear();
            CurrentMovieInfo.MovSamplesLocalUri.ForEach(s =>
            {
                string newurl = Path.Combine(dir_msample.FullName, Path.GetFileName(s.LocalPath));
                File.Copy(s.LocalPath, newurl);
                samples_newuri.Add(new Uri(newurl));
            });
            CurrentMovieInfo.MovSamplesLocalUri.Clear();
            CurrentMovieInfo.MovSamplesLocalUri.AddRange(samples_newuri);

            //re-binding list_CurrentSamples
            CurrentMovieInfo.MovSamplesLocalUri.ForEach(s => CurrentMovieSamples.Add(new MovieSample
            {
                MS_Uri = s,
                MS_Name = Path.GetFileNameWithoutExtension(s.LocalPath),
                MS_Index = CurrentMovieSamples.Count
            }));
            list_CurrentSamples.ItemsSource = CurrentMovieSamples;

            CurrentMovieInfo.SourcePath = root_mi;
            XGlobal.CurrentContext.TotalMovies.Add(CurrentMovieInfo);
            Func_SaveContext();

            list_ProcessInformation.SelectedIndex = list_ProcessInformation.Items.Add($"存儲[{CurrentMovieInfo.ReleaseID}]記錄完成.");
        }

        private void Func_SaveContext()
        {
            if (File.Exists(XGlobal.DataFile)) File.Move(XGlobal.DataFile, $"{XGlobal.DataFile}.{DateTime.Now.ToString("yyyyMMddHHmmss")}");
            BinaryFormatter binf = new BinaryFormatter();
            using (FileStream fs = new FileStream(XGlobal.DataFile, FileMode.Create)) { binf.Serialize(fs, XGlobal.CurrentContext); }
        }

        private async void Command_SelectResult_Executed(object sender, ExecutedRoutedEventArgs e)
        {
            group_SearchResult.State = GroupBoxState.Minimized;
            //訪問目標頁面
            list_ProcessInformation.SelectedIndex = list_ProcessInformation.Items.Add($"讀取影片 {((GalleryItem)e.Parameter).Tag} ...");

            CurrentMovieInfo.OfficialWeb = new Uri(((GalleryItem)e.Parameter).Tag.ToString());
            var streamresult = await XGlobal.FnReadWebData(CurrentMovieInfo.OfficialWeb);
            CurrentHtmlDocument.LoadHtml(streamresult.Replace(Environment.NewLine, " ").Replace("\t", " "));
            HtmlNode hnode = CurrentHtmlDocument.DocumentNode;

            HtmlNode _errornode = hnode.SelectSingleNode("//div[@class='alert alert-block alert-error']");
            if (_errornode != null)
            {
                MessageBox.Show(_errornode.InnerText, "搜索頁面加載錯誤");
                txt_Keywords.Focus();
                return;
            }

            hnode = hnode.SelectSingleNode("/html/body/div[2]");
            foreach (HtmlNode _txt_node in hnode.SelectNodes(".//text()"))
                if (!Regex.IsMatch(_txt_node.InnerText, @"\S", RegexOptions.Singleline)) _txt_node.Remove();

            txt_ReleaseName.EditValue = CurrentMovieInfo.ReleaseName = hnode.SelectSingleNode("//h3[1]").InnerText;
            group_ProcessInformation.Header = $"PROCESS INFORMATION: [{CurrentMovieInfo.ReleaseName}]";

            //處理 Mov Info
            Func_Analysis_Movie(hnode);

            //刪除重複 Mov            
            if (XGlobal.CurrentContext.TotalMovies.Exists(m => m.ReleaseID == CurrentMovieInfo.ReleaseID))
            {
                if (MessageBox.Show(
                    String.Format(
                    "影片 [{0}] 已有歸類存檔({1}*{2}, {3})，操作取消，刪除影片文件嗎？",
                    CurrentMovieInfo.ReleaseID,
                    CurrentMovieInfo.VWidth,
                    CurrentMovieInfo.VHeight,
                    XGlobal.Format_MachineSize(CurrentMovieInfo.MediaFilesTotalSize)),
                    "影片已歸檔",
                    MessageBoxButton.YesNo) == MessageBoxResult.Yes)
                {
                    XGlobal.DeleteMovies(CurrentMovieInfo.SourcePath, CurrentMovieInfo.SourceMediaFileExt);
                    list_ProcessInformation.SelectedIndex = list_ProcessInformation.Items.Add($"源目錄 {CurrentMovieInfo.SourcePath} 已刪除.");
                    return;
                }
                else
                {
                    list_ProcessInformation.SelectedIndex = list_ProcessInformation.Items.Add($"影片 [{CurrentMovieInfo.ReleaseID}] 已有歸類存檔，操作取消.");
                    return;
                }
            }
            
            //處理 Stars 段
            if (!await Func_Analysis_Stars(hnode)) return;

            //處理Cover
            if (!await Func_AnalysisMovie_Cover(hnode)) return;

            //處理 Sample Images 段
            if (!await Func_AnalysisMovie_Samples(hnode)) return;
            //bnMoveMovie.Enabled = true;
            IsReadyProcess = true;
            CommandManager.InvalidateRequerySuggested();
        }

        private void FindCmd_CanExecute(object sender, CanExecuteRoutedEventArgs e)
        {
            e.CanExecute = true;
            e.Handled = true;
        }

        private void OpenCmd_CanExecute(object sender, CanExecuteRoutedEventArgs e)
        {
            e.CanExecute = true;
            e.Handled = true;
        }

        private void FindCmd_Executed(object sender, ExecutedRoutedEventArgs e)
        {
            if (e.Parameter.ToString() == "C") Func_FindDB(XGlobal.CurrentContext.CSite);
            if (e.Parameter.ToString() == "U") Func_FindDB(XGlobal.CurrentContext.USite);
        }

        public List<object> Object_SearchResultList = new List<object>();
        /// <summary>
        /// 僅用於JAVZOO/JAVPEE的搜索
        /// </summary>
        /// <param name="SPKEY">javzoo(avsow.net)和javpee(avmask.net)選一</param>
        private async void Func_FindDB(string SPKEY)
        {
            CurrentSearchKey = SPKEY;
            //list_ProcessInformation.Items.Clear();
            XGlobal.RebuildSubDirTemp();

            list_ProcessInformation.SelectedIndex = list_ProcessInformation.Items.Add($"從 {SPKEY.ToUpper()} 搜索 {txt_Keywords.Text}...");

            Uri uri_search = new Uri($"https://{SPKEY}/ja/search/{WebUtility.UrlEncode(txt_Keywords.Text)}");
            var streamresult = await XGlobal.FnReadWebData(uri_search);

            if (streamresult.Contains("System.Net.Http.HttpRequestException:"))
            {
                System.Diagnostics.Debug.WriteLine(uri_search.ToString());
                System.Diagnostics.Debug.WriteLine(streamresult);
                return;
            }

            CurrentHtmlDocument.LoadHtml(streamresult);
            HtmlNode hnode = CurrentHtmlDocument.DocumentNode;

            HtmlNode _errornode = hnode.SelectSingleNode("//div[@class='alert alert-block alert-error']");
            if (_errornode != null)
            {
                MessageBox.Show(_errornode.InnerText, "Key Words Mismatch");
                txt_Keywords.Focus();
                return;
            }

            list_ProcessInformation.SelectedIndex = list_ProcessInformation.Items.Add($"從 {SPKEY.ToUpper()} 返回 {txt_Keywords.Text} 相關影片...");

            //搜索結果列表
            //frmSelectMovies fsm = new frmSelectMovies(txtKeywords.Text, hdoc, uri_search, SPKEY);
            #region 讀取搜索結果
            HtmlNode errornode = CurrentHtmlDocument.DocumentNode.SelectSingleNode("//div[@class='container-fluid']/div[@class='alert alert-danger']");
            if (errornode != null)
            {
                gallery_SearchResult.Gallery.Groups[0].Caption = errornode.SelectSingleNode("./h4").InnerText;
                return;
            }

            HtmlNodeCollection node_results = CurrentHtmlDocument.DocumentNode.SelectNodes("//div[@class='item']");

            gallery_SearchResult.Gallery.Groups[0].Items.Clear();
            gallery_SearchResult.Gallery.Groups[0].Caption = $"Searched with [{txt_Keywords.Text}], {node_results.Count} results returned:";

            foreach (HtmlNode _node in node_results)
            {
                Stream tempimg = await XGlobal.FnReadWebStream(_node.SelectSingleNode(".//img").Attributes["src"].Value, uri_search);
                GalleryItem gi = new GalleryItem();
                if (tempimg != null)
                {
                    gi.Caption = $"{_node.SelectSingleNode(".//date[1]").InnerText} / {_node.SelectSingleNode(".//date[2]").InnerText}";
                    _node.SelectSingleNode(".//span[1]").RemoveAll();
                    gi.Description = _node.SelectSingleNode(".//span[1]").InnerText;
                    //gi.Glyph = new BitmapImage() { StreamSource = tempimg };
                    gi.Glyph = new ImageSourceConverter().ConvertFrom(tempimg) as ImageSource;
                    gi.Tag = XGlobal.UrlCheck(_node.SelectSingleNode(".//a[1]").Attributes["href"].Value);
                    gallery_SearchResult.Gallery.Groups[0].Items.Add(gi);
                }
                else
                {
                    gi.Caption = $"{_node.SelectSingleNode(".//date[1]").InnerText} / {_node.SelectSingleNode(".//date[2]").InnerText}";
                    _node.SelectSingleNode(".//span[1]").RemoveAll();
                    gi.Description = _node.SelectSingleNode(".//span[1]").InnerText;
                    gi.Glyph = new BitmapImage(new Uri("Resources/404.png", UriKind.Relative));
                    gi.Tag = XGlobal.UrlCheck(_node.SelectSingleNode(".//a[1]").Attributes["href"].Value);
                    gallery_SearchResult.Gallery.Groups[0].Items.Add(gi);
                }
                gi.Command = Command_SelectResult;
                gi.CommandTarget = list_ProcessInformation;
                gi.CommandParameter = gi;
            }//end foreach in node_results
            #endregion


        }

        private void OpenCmd_Executed(object sender, ExecutedRoutedEventArgs e)
        {
            IsReadyProcess = false;
            CurrentMovieInfo = new MovieInfo();
            group_Sample.State = GroupBoxState.Minimized;
            InitializeUIControls();

            Microsoft.Win32.OpenFileDialog ofd_selectmovie = new Microsoft.Win32.OpenFileDialog();
            string path_source = string.Empty;
            if (path_source != string.Empty) ofd_selectmovie.InitialDirectory = path_source;
            ofd_selectmovie.Filter = "Video files|*.avi;*.wmv;*.mp4;*.m4v;*.asf；*.asx;*.rm;*.rmvb;*.mpg;*.mpeg;*.mpe;*.3gp;*.mov;*.dat;*.mkv;*.flv;*.vob";
            Nullable<bool> result = ofd_selectmovie.ShowDialog();

            if (result.HasValue && result.Value)
            {
                Func_ProcessSelectFileString(ofd_selectmovie.FileName);
                path_source = Path.GetDirectoryName(ofd_selectmovie.FileName);
                txt_NewMovieFullName.Text = ofd_selectmovie.FileName;
            }
        }

        private void Func_ProcessSelectFileString(string _filename)
        {
            CurrentMovieInfo.AnalysisMediaFiles(_filename);
            lbl_MediaFileInfo.Text = CurrentMovieInfo.MediaFilesDecodeDesc;
            //mi_current.SourcePath = new DirectoryInfo(Path.GetDirectoryName(_filename));
            txt_NewMovieFullName.Text = _filename;
            txt_NewMovieFullName.Items.Add(_filename);

            //mi_current.SourceMediaFileExt = Path.GetExtension(_filename);
            //mi_current.TotalMoviesSize = XHelper.IOHelper_GetMoviesTotalSize(mi_current.SourcePath, mi_current.SourceMediaFileExt);

            txt_Keywords.Items.Clear();

            Match _match_heyzo = Regex.Match(_filename, @"hey.*?(\d+)", RegexOptions.Singleline | RegexOptions.IgnoreCase);
            Match _match_caribbean = Regex.Match(_filename, @"carib.*?(\d+[_-]\d+)", RegexOptions.Singleline | RegexOptions.IgnoreCase);
            Match _match_1pondo = Regex.Match(_filename, @"1pondo.*?(\d+[_-]\d+)", RegexOptions.Singleline | RegexOptions.IgnoreCase);
            Match _match_nums = Regex.Match(_filename, @"(\d{6,}[_-]\d{3,})", RegexOptions.Singleline | RegexOptions.IgnoreCase);
            Match _match_3d2d = Regex.Match(_filename, @"([a-z|A-Z]+3d(?:2d)?[a-z|A-Z]*\-?\d+)", RegexOptions.Singleline | RegexOptions.IgnoreCase);
            Match _match_bd = Regex.Match(_filename, @"([a-z|A-Z]+bd\-?[a-z|A-Z]?\d+)", RegexOptions.Singleline | RegexOptions.IgnoreCase);
            Match _match_tokyohot = Regex.Match(_filename, @"(n\d{4,})", RegexOptions.Singleline | RegexOptions.IgnoreCase);
            //(ps\d|set|lfm|pss|lovu|xms|ykt|swm)_\d+?_[a-z]+\d*
            Match _match_scute1 = Regex.Match(_filename, @"((?:\d+?|set)_[a-z]+?_\d+)", RegexOptions.Singleline | RegexOptions.IgnoreCase);
            Match _match_scute2 = Regex.Match(_filename, @"((?:ps\d|set|lfm|pss|lovu|xms|ykt|swm)_\d+?_[a-z]+\d*)", RegexOptions.Singleline | RegexOptions.IgnoreCase);

            bn_SearchSOX.UpdateDefaultStyle();
            bn_SearchMOO.UpdateDefaultStyle();

            var greenbrush = new SolidColorBrush(Colors.Green);
            if (_match_heyzo.Success)
            {
                txt_Keywords.Items.Add("heyzo " + _match_heyzo.Groups[1].Value);
                bn_SearchSOX.Foreground = greenbrush;
                IsCensored = false;
            }
            else if (_match_caribbean.Success)
            {
                txt_Keywords.Items.Add(_match_caribbean.Groups[1].Value);
                bn_SearchSOX.Foreground = new SolidColorBrush(Colors.Green);
                bn_SearchMOO.UpdateDefaultStyle();
            }
            else if (_match_1pondo.Success)
            {
                txt_Keywords.Items.Add(_match_1pondo.Groups[1].Value);
                bn_SearchSOX.Foreground = greenbrush;
                IsCensored = false;
            }
            else if (_match_nums.Success)
            {
                txt_Keywords.Items.Add(_match_nums.Groups[1].Value);
                bn_SearchSOX.Foreground = greenbrush;
                IsCensored = false;
            }
            else if (_match_3d2d.Success)
            {
                txt_Keywords.Items.Add(_match_3d2d.Groups[1].Value);
                bn_SearchSOX.Foreground = greenbrush;
                IsCensored = false;
            }
            else if (_match_bd.Success)
            {
                txt_Keywords.Items.Add(_match_bd.Groups[1].Value);
                bn_SearchSOX.Foreground = greenbrush;
                IsCensored = false;
            }
            else if (_match_tokyohot.Success)
            {
                txt_Keywords.Items.Add(_match_tokyohot.Groups[1].Value);
                bn_SearchSOX.Foreground = greenbrush;
                IsCensored = false;
            }
            else
            {
                //MatchCollection _mc = Regex.Matches(System.IO.Path.GetFileNameWithoutExtension(ofd.FileName), @"([a-z|A-Z]+\-?\d+)", RegexOptions.Singleline | RegexOptions.IgnoreCase);
                MatchCollection _mc = Regex.Matches(_filename, @"([a-z|A-Z]+\-?\d+)", RegexOptions.Singleline | RegexOptions.IgnoreCase);
                foreach (Match _m in _mc)
                {
                    if (_m.Groups[1].Value == "whole1") continue;
                    if (_m.Groups[1].Value == "hd1") continue;
                    if (_m.Groups[1].Value == "mp4") continue;
                    txt_Keywords.Items.Add(Regex.Replace(_m.Groups[1].Value, @"0+", "0"));
                }
                //_mc = Regex.Matches(System.IO.Path.GetFileNameWithoutExtension(ofd.FileName), @"(\d+[_-]\d+)|(\d+_[a-z|A-Z]+_\d+)", RegexOptions.Singleline | RegexOptions.IgnoreCase);
                _mc = Regex.Matches(_filename, @"(\d+[_-]\d+)|(\d+_[a-z|A-Z]+_\d+)", RegexOptions.Singleline | RegexOptions.IgnoreCase);
                foreach (Match _m in _mc) txt_Keywords.Items.Add(_m.Groups[1].Value);
                bn_SearchSOX.UpdateDefaultStyle();
                bn_SearchMOO.Foreground = greenbrush;
            }
            txt_Keywords.SelectedIndex = 0;

            InitializeUIControls();

        }

        private void Func_Analysis_Movie(HtmlNode sourcenode)
        {
            list_ProcessInformation.SelectedIndex = list_ProcessInformation.Items.Add("讀取影片信息...");

            HtmlNode info_node = sourcenode.SelectSingleNode("//div[@class='col-md-3 info']");
            string mov_desc = info_node.InnerHtml;
            mov_desc = Regex.Replace(mov_desc, @"<span class=""genre"">(.*?)</span>", @"[$1]");
            mov_desc = Regex.Replace(mov_desc, @"<a.*?>(.*?)</a>", "$1");
            mov_desc = Regex.Replace(mov_desc, @"<h\d>(.*?)</h\d>", "$1");
            mov_desc = Regex.Replace(mov_desc, @"<span.*?>(.*?)</span>", "$1");
            mov_desc = Regex.Replace(mov_desc, @"<p.*?>(.*?)</p>", "$1\r\n");

            foreach (HtmlNode _node in info_node.SelectNodes(".//*[@class='header']"))
            {
                /* en site
                if (_node.InnerText.Contains("ID")) txtReleaseID.Text = _node.NextSibling.InnerText;
                else if (_node.InnerText.Contains("Release Date")) txtReleaseDate.Text = _node.ParentNode.InnerText;
                */
                /* tw site
                if (_node.InnerText.Contains("識別碼")) txtReleaseID.Text = _node.NextSibling.InnerText;
                else if (_node.InnerText.Contains("發行日期")) txtReleaseDate.Text = Regex.Replace(_node.ParentNode.InnerText, @"(.*?\:)", "");
                */
                /* ja site */
                if (_node.InnerText.Contains("品番")) txt_ReleaseID.Text = CurrentMovieInfo.ReleaseID = _node.NextSibling.InnerText.Trim();
                else if (_node.InnerText.Contains("発売日"))
                {
                    txt_ReleaseDate.Text = Regex.Replace(_node.ParentNode.InnerText, @"(.*?\:)", "");
                    CurrentMovieInfo.ReleaseDate = DateTime.Parse(txt_ReleaseDate.Text);
                }
            }
        }

        private async Task<bool> Func_Analysis_Stars(HtmlNode sourcenode)
        {
            CurrentStars.Clear();
            list_ProcessInformation.SelectedIndex = list_ProcessInformation.Items.Add("Reading stars informations...");
            list_CurrentStars.ItemsSource = null;
            //list_CurrentStars.IsEnabled = false;

            HtmlNodeCollection _hncc = sourcenode.SelectNodes("//a[@class='avatar-box']");

            string name_en = string.Empty;
            string name_ja = string.Empty;
            CurrentMovieInfo.ActorUIDs = new List<Guid>();
            //list_CurrentStars.ItemsSource = CurrentStars;

            if (_hncc == null)
            {
                MessageBox.Show($"No movie information from{CurrentSearchKey.ToUpper()}.\nTry other web source.", "數據缺損");
                //return false;
            }
            else
                foreach (HtmlNode _node in _hncc)
                {
                    bool is_star_stored = true;
                    name_ja = XGlobal.IllegalFiltered(_node.SelectSingleNode(".//span").InnerText.Trim());
                    StarInfo star = XGlobal.CurrentContext.TotalStars.Find(s => s.JName == name_ja);
                    if (star is null)
                    {
                        star = new StarInfo(name_ja);
                        is_star_stored = false;
                    }

                    //Get EName
                    star.OfficialWeb_JA = XGlobal.UrlCheck(_node.Attributes["href"].Value);
                    star.OfficialWeb_EN = XGlobal.UrlCheck(Regex.Replace(star.OfficialWeb_JA, @"/ja/", @"/en/"));
                    var streamstarresult = await XGlobal.FnReadWebData(star.OfficialWeb_EN);
                    HtmlDocument hstardoc = new HtmlDocument();
                    hstardoc.LoadHtml(streamstarresult);
                    star.EName = name_en = hstardoc.DocumentNode.SelectSingleNode("//div[@id='waterfall']//span[@class='pb-10']").InnerText.Trim();
                    //Read Avator to Stream                    
                    list_ProcessInformation.SelectedIndex = list_ProcessInformation.Items.Add($"Create star directory [{star.JName}] (TEMP)...");

                    star.AvatorWebUri = new Uri(XGlobal.UrlCheck(_node.SelectSingleNode(".//img").Attributes["src"].Value));
                    //star.CreateStarDirectoryTemp();
                    star.CreateLocalStarDirectory(CurrentMovieInfo);

                    Stream temp = await XGlobal.FnReadWebStream(star.AvatorWebUri, CurrentMovieInfo.OfficialWeb);
                    star.AvatorFileInfo = new FileInfo(Path.Combine(
                        star.LastLocation.FullName,
                        star.AvatorWebUri.Segments[star.AvatorWebUri.Segments.Length - 1]));

                    using (FileStream sourceStream = new FileStream(star.AvatorFileInfo.FullName, FileMode.Create, FileAccess.Write, FileShare.None, bufferSize: 4096, useAsync: true))
                    {
                        await temp.CopyToAsync(sourceStream);
                        await sourceStream.FlushAsync();
                    }

                    //list_CurrentStars.Images.Add(Image.FromStream(temp));

                    star.StoredMovieIDs.Add(CurrentMovieInfo.ReleaseID);
                    CurrentStars.Add(star);
                    CurrentMovieInfo.ActorUIDs.Add(star.UniqueID);
                    if (!is_star_stored) XGlobal.CurrentContext.TotalStars.Add(star);
                }
            if (CurrentStars.Count > 0)
            {
                list_CurrentStars.ItemsSource = CurrentStars;
            }

            list_CurrentStars.IsEnabled = true;
            return true;
        }

        private void list_CurrentStars_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if ((sender as FlipView).ItemsSource is null) return;
            txt_StarNameJA.Text = (e.AddedItems[0] as StarInfo).JName;
            txt_StarNameEN.Text = (e.AddedItems[0] as StarInfo).EName;
        }

        private async Task<bool> Func_AnalysisMovie_Cover(HtmlNode sourcenode)
        {
            CurrentMovieInfo.CoverWebUri = new Uri(XGlobal.UrlCheck(sourcenode.SelectSingleNode("//a[@class='bigImage']/img").Attributes["src"].Value));
            CurrentMovieInfo.CoverFileInfo = new FileInfo(Path.Combine(
                CurrentMovieInfo.SourcePath.FullName, 
                CurrentMovieInfo.CoverWebUri.Segments[CurrentMovieInfo.CoverWebUri.Segments.Length - 1]));
            //CurrentMovieInfo.CoverDirectoryInfo = XGlobal.CurrentContext.DirCoversTemp;

            list_ProcessInformation.SelectedIndex = list_ProcessInformation.Items.Add($"創建影片封面 {CurrentMovieInfo.CoverFileInfo.FullName} ...");
            txt_MovieCoverUrl.EditValue = CurrentMovieInfo.CoverWebUri.ToString();
            var coverimage = new ImageSourceConverter().ConvertFrom(CurrentMovieInfo.CoverWebUri);
            img_MovieCover.Source = coverimage as ImageSource;

            using (Stream temp = await XGlobal.FnReadWebStream(CurrentMovieInfo.CoverWebUri, CurrentMovieInfo.OfficialWeb))
            {
                if (temp != Stream.Null)
                {
                    if (CurrentMovieInfo.CoverFileInfo.Exists) CurrentMovieInfo.CoverFileInfo.Delete();
                    using (FileStream sourceStream = new FileStream(
                        CurrentMovieInfo.CoverFileInfo.FullName, 
                        FileMode.Create, 
                        FileAccess.Write, 
                        FileShare.ReadWrite, 
                        bufferSize: 4096, 
                        useAsync: true))
                    {
                        temp.Seek(0, SeekOrigin.Begin);
                        await temp.CopyToAsync(sourceStream);
                        await sourceStream.FlushAsync();
                    }
                }
            }
            return true;
        }

        private async Task<bool> Func_AnalysisMovie_Samples(HtmlNode sourcenode)
        {
            CurrentSamplesLocation = XGlobal.CurrentContext.DirAppTemp.CreateSubdirectory(Guid.NewGuid().ToString("N"));
            list_ProcessInformation.SelectedIndex = list_ProcessInformation.Items.Add($"創建影片劇照目錄 {CurrentSamplesLocation.Name} ...");
            CurrentMovieInfo.MovSamplesWebUri = new List<Uri>();
            CurrentMovieInfo.MovSamplesLocalUri = new List<Uri>();
            foreach (DirectoryInfo _di in CurrentMovieInfo.SourcePath.GetDirectories())
                if (_di.Name.ToLower().Contains("image") || _di.Name.ToLower().Contains("gal") || _di.Name.ToLower().Contains("sample"))
                {
                    CurrentMovieInfo.MovSamplesLocalUri.AddRange(_di.EnumerateFiles("*.jpg", SearchOption.AllDirectories)
                        .Select(fi => new Uri(fi.FullName)));
                    list_ProcessInformation.SelectedIndex = list_ProcessInformation.Items.Add($"讀取影片SAMPLE目錄 {_di.Name} ...");
                }
                else
                    list_ProcessInformation.SelectedIndex = list_ProcessInformation.Items.Add($"影片無SAMPLE目錄 ...");

            foreach (FileInfo _fi in 
                CurrentMovieInfo.SourcePath.EnumerateFiles("*.zip", SearchOption.TopDirectoryOnly)
                .Union(CurrentMovieInfo.SourcePath.EnumerateFiles("*.rar", SearchOption.TopDirectoryOnly)))
            {
                //CurrentMovieInfo.SourcePath.CreateSubdirectory("@SAMPLE");
                //string _ssdir = Path.Combine(_fi.DirectoryName, "@SAMPLE");
                XGlobal.UnRAR(CurrentSamplesLocation.FullName, _fi.DirectoryName, _fi.Name);

                CurrentMovieInfo.MovSamplesLocalUri.AddRange(CurrentSamplesLocation.EnumerateFiles("*.jpg", SearchOption.AllDirectories)
                    .Select(fi => new Uri(fi.FullName)));
                list_ProcessInformation.SelectedIndex = list_ProcessInformation.Items.Add($"解壓SAMPLE文件目錄 {_fi.Name} ...");
            }

            if (XGlobal.CurrentContext.DirSamplesTemp.GetFiles("*.jpg").Length == 0)
            {
                HtmlNode sample_node = sourcenode.SelectSingleNode("//div[@id='sample-waterfall']");
                if (sample_node != null)
                    if (sample_node.PreviousSibling.InnerText.Contains("サンプル画像"))
                    {
                        list_CurrentSamples.Items.Clear();//TODO
                        foreach (HtmlNode _node in sample_node.SelectNodes("./a"))
                        {
                            Uri _uri_sample;
                            if (_node.Attributes["href"] != null) _uri_sample = new Uri(XGlobal.UrlCheck(_node.Attributes["href"].Value));
                            else _uri_sample = new Uri(XGlobal.UrlCheck(_node.SelectSingleNode("//img").Attributes["src"].Value));
                            CurrentMovieInfo.MovSamplesWebUri.Add(_uri_sample);
                            string _strsample = Path.Combine(
                                CurrentSamplesLocation.FullName, 
                                _uri_sample.Segments[_uri_sample.Segments.Length - 1]);

                            list_ProcessInformation.SelectedIndex = list_ProcessInformation.Items.Add($"創建影片劇照 {_strsample} ...");
                            using (Stream temp = await XGlobal.FnReadWebStream(_uri_sample, CurrentMovieInfo.OfficialWeb))
                            {
                                if (temp != Stream.Null)
                                {
                                    using (FileStream sourceStream = new FileStream(_strsample, FileMode.Create, FileAccess.Write, FileShare.None, bufferSize: 4096, useAsync: true))
                                    {
                                        await temp.CopyToAsync(sourceStream);
                                        await sourceStream.FlushAsync();
                                        CurrentMovieInfo.MovSamplesLocalUri.Add(new Uri(_strsample,UriKind.Absolute));
                                    }
                                }
                            }
                        }

                    }
            }

            CurrentMovieSamples.Clear();
            CurrentMovieInfo.MovSamplesLocalUri.ForEach(s => CurrentMovieSamples.Add(new MovieSample
            {
                MS_Uri = s,
                MS_Name = Path.GetFileNameWithoutExtension(s.LocalPath),
                MS_Index = CurrentMovieSamples.Count
            }));
            list_CurrentSamples.ItemsSource = CurrentMovieSamples;
            list_ProcessInformation.SelectedIndex = list_ProcessInformation.Items.Add("影片處理完成.");
            return true;
        }

        public class MovieSample
        {
            public Uri MS_Uri { get; set; }
            public int MS_Index { get; set; }
            public string MS_Name { get; set; }
        }

        private void list_CurrentSamples_MouseDown(object sender, MouseButtonEventArgs e)
        {
            group_Sample.State = GroupBoxState.Normal;
            img_MovieSample.Source = new ImageSourceConverter()
                .ConvertFrom((list_CurrentSamples.SelectedItem as MovieSample).MS_Uri)
                as ImageSource;
        }

        private void group_Sample_StateChanged(object sender, ValueChangedEventArgs<GroupBoxState> e)
        {
            if (e.NewValue == GroupBoxState.Minimized) group_Cover.State = GroupBoxState.Normal;
            if (e.NewValue == GroupBoxState.Normal) group_Cover.State = GroupBoxState.Minimized;
        }
    }
}
