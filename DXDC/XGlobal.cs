using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Runtime.Serialization.Formatters.Binary;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Data;
using System.Windows.Media.Imaging;

namespace DXDC
{
    [Serializable]
    public static class XGlobal
    {
        public static XContext CurrentContext { get; set; }
        public static HttpClient CurrentHttpClient { get; set; }
        public static CancellationTokenSource CurrentCTS { get; set; } = new CancellationTokenSource();
        public const string DataFile = @"D:\[BACKUPs]\DATA\context.s";
        public const string ArchiveName = "DATA.COLLECTION";

        static XGlobal()
        {
            CurrentContext = new XContext();

            ServicePointManager.DefaultConnectionLimit = 512;

            HttpClientHandler handler = new HttpClientHandler()
            {
                AutomaticDecompression = DecompressionMethods.GZip,
                UseProxy = CurrentContext.IsUseProxy,
                Proxy = (CurrentContext.IsUseProxy) ? CurrentContext.SSProxy : null
            };


            CurrentHttpClient = new HttpClient(handler);
            CurrentHttpClient.MaxResponseContentBufferSize = 1024 * 1024;
            CurrentHttpClient.DefaultRequestHeaders.Add("user-agent", "Mozilla/5.0 (compatible; MSIE 10.0; Windows NT 6.1; WOW64; Trident/6.0)");
            CurrentHttpClient.DefaultRequestHeaders.Add("Accept-Language", "en-US,en;q=0.8,zh-Hans-CN;q=0.5,zh-Hans;q=0.3");

            BinaryFormatter binf = new BinaryFormatter();
            //Initialize CurrentContext
            if (File.Exists(DataFile))
                using (FileStream fs = new FileStream(DataFile, FileMode.Open)) CurrentContext = (XContext)binf.Deserialize(fs);
                

        }

        #region Utils

        /// <summary>
        /// Get string data from web
        /// </summary>
        /// <param name="uripage">web address uri</param>
        /// <param name="referrer">web request header referrer</param>
        /// <returns></returns>
        public static async Task<string> FnReadWebData(Uri uripage, Uri referrer)
        {
            try
            {
                CurrentHttpClient.DefaultRequestHeaders.Referrer = referrer;
                var resp = await CurrentHttpClient.GetAsync(uripage, CurrentCTS.Token);

                if (resp.StatusCode == HttpStatusCode.OK)
                {
                    resp.EnsureSuccessStatusCode();
                    return await resp.Content.ReadAsStringAsync();
                }
                else return resp.StatusCode.ToString();
                //}
            }
            catch (HttpRequestException hre)
            {
                return hre.ToString();
            }
            catch (Exception ex)
            {
                return ex.ToString();
            }

        }

        public static async Task<string> FnReadWebData(string urlpage, Uri referrer)
        {
            UrlCheck(ref urlpage);
            return await FnReadWebData(new Uri(urlpage), referrer);
        }

        public static async Task<string> FnReadWebData(Uri uripage)
        {
            return await FnReadWebData(uripage, uripage);
        }

        public static async Task<string> FnReadWebData(string urlpage)
        {
            UrlCheck(ref urlpage);
            return await FnReadWebData(new Uri(urlpage));
        }

        /// <summary>
        /// Get stream data from web
        /// </summary>
        /// <param name="uriimage">web address uri</param>
        /// <param name="referrer">web request header referrer</param>
        /// <returns></returns>
        public static async Task<Stream> FnReadWebStream(Uri uriimage, Uri referrer)
        {
            CurrentHttpClient.DefaultRequestHeaders.Referrer = referrer;
            var resp = await CurrentHttpClient.GetAsync(uriimage, CurrentCTS.Token);

            if (resp.StatusCode == HttpStatusCode.OK)
            {
                resp.EnsureSuccessStatusCode();
                return await resp.Content.ReadAsStreamAsync();
            }
            else
            {
                return Stream.Null;
            }
            //}

        }

        public static async Task<Stream> FnReadWebStream(string urlimage, Uri referrer)
        {
            UrlCheck(ref urlimage);
            return await FnReadWebStream(new Uri(urlimage), referrer);
        }

        public static void UrlCheck(ref string url)
        {
            if (url.StartsWith("//")) url = url.Insert(0, "http:");
        }

        public static string UrlCheck(string url)
        {
            if (url.StartsWith("//"))
                return url.Insert(0, "http:");
            else
                return url;
        }

        public static String Format_MachineSize(long msize)
        {
            if (msize < 0)
            {
                throw new ArgumentOutOfRangeException("machine size");
            }
            else if (msize >= 1024 * 1024 * 1024) //大小大于或等于1024M
            {
                return string.Format("{0:0.00} G", (double)msize / (1024 * 1024 * 1024));
            }
            else if (msize >= 1024 * 1024) //大小大于或等于1024K
            {
                return string.Format("{0:0.00} M", (double)msize / (1024 * 1024));
            }
            else if (msize >= 1024) //大小大于等于1024
            {
                return string.Format("{0:0.00} K", (double)msize / 1024);
            }
            else
            {
                return string.Format("{0:0.00}", msize);
            }
        }

        public static String IllegalFiltered(String SourceString)
        {
            string regexSearch = new string(Path.GetInvalidFileNameChars()) + new string(Path.GetInvalidPathChars());
            Regex r = new Regex(string.Format("[{0}]", Regex.Escape(regexSearch)));
            return r.Replace(SourceString, "");
        }

        /// <summary>
        /// 刪除目錄下的所有指定後綴名的視頻文件
        /// </summary>
        /// <param name="sourcedi">源目錄</param>
        /// <param name="ext">文件後綴名</param>
        public static void DeleteMovies(DirectoryInfo sourcedi, string ext)
        {
            if (sourcedi.Exists)
            {
                List<FileInfo> files = sourcedi.GetFiles("*" + ext, System.IO.SearchOption.TopDirectoryOnly).Where(f => f.Name.EndsWith(ext, StringComparison.CurrentCultureIgnoreCase)).ToList();
                files.ForEach(c => c.Delete());
            }
            else
            {
                throw new DirectoryNotFoundException(String.Format("源目录{0}不存在，可能已被刪除！", sourcedi.FullName));
            }
        }

        /// <summary>
        /// 移动文件夹中的所有文件夹与文件到另一个文件夹
        /// </summary>
        /// <param name="sourcePath">源文件夹</param>
        /// <param name="destPath">目标文件夹</param>
        public static void MoveFolder(DirectoryInfo sourcePath, DirectoryInfo destPath)
        {
            if (sourcePath.Exists)
            {
                if (!destPath.Exists)
                {
                    //目标目录不存在则创建
                    try
                    {
                        destPath.Create();
                    }
                    catch (Exception ex)
                    {
                        throw new Exception("创建目标目录失败：" + ex.Message);
                    }
                }
                //获得源文件下所有文件
                List<FileInfo> files = new List<FileInfo>(sourcePath.EnumerateFiles());
                files.ForEach(c =>
                {
                    string destFile = Path.Combine(destPath.FullName, c.Name);
                    //覆盖模式
                    if (File.Exists(destFile))
                    {
                        File.Delete(destFile);
                    }
                    c.MoveTo(destFile);
                });
                //获得源文件下所有目录文件
                List<DirectoryInfo> folders = new List<DirectoryInfo>(sourcePath.EnumerateDirectories());

                folders.ForEach(c =>
                {
                    string destDir = Path.Combine(destPath.FullName, c.Name);
                    //采用递归的方法实现
                    c.MoveTo(destDir);
                });
            }
            else
            {
                throw new DirectoryNotFoundException("源目录不存在！");
            }
        }

        /// <summary>
        /// 移动文件夹中的所有文件夹与文件到另一个文件夹
        /// </summary>
        /// <param name="sourcePath">源文件夹</param>
        /// <param name="destPath">目标文件夹</param>
        public static void MoveImageFolder(string sourcePath, string destPath, string ext)
        {
            if (Directory.Exists(sourcePath))
            {
                if (!Directory.Exists(destPath))
                {
                    //目标目录不存在则创建
                    try
                    {
                        Directory.CreateDirectory(destPath);
                    }
                    catch (Exception ex)
                    {
                        throw new Exception("创建目标目录失败：" + ex.Message);
                    }
                }
                //获得源文件下所有文件
                List<string> files = new List<string>(Directory.GetFiles(sourcePath, "*" + ext, System.IO.SearchOption.AllDirectories));
                files.ForEach(c =>
                {
                    string destFile = Path.Combine(new string[] { destPath, Path.GetFileName(c) });
                    //覆盖模式
                    if (File.Exists(destFile))
                    {
                        File.Delete(destFile);
                    }
                    GC.Collect();
                    GC.WaitForPendingFinalizers();
                    File.Move(c, destFile);
                });
                Directory.Delete(sourcePath, true);
            }
            else
            {
                throw new DirectoryNotFoundException("源目录不存在！");
            }
        }
        public static void MoveImageFolder(DirectoryInfo dirsource, DirectoryInfo dirdest, string ext)
        {
            if(dirsource.Exists)
            {
                if(!dirdest.Exists)
                    try { dirdest.Create(); } catch (Exception ex) { throw new Exception("创建目标目录失败：" + ex.Message); }

                List<FileInfo> files = new List<FileInfo>(dirsource.GetFiles("*" + ext, SearchOption.AllDirectories));
                files.ForEach(c =>
                    {
                        string strfiledest = Path.Combine(dirdest.FullName, c.Name);
                        if (File.Exists(strfiledest)) File.Delete(strfiledest);
                        GC.Collect();
                        GC.WaitForPendingFinalizers();
                        c.MoveTo(strfiledest);
                    });
                dirsource.Delete(true);
            }
        }

        /// <summary>
        /// 利用 WinRAR 进行压缩
        /// </summary>
        /// <param name="path">将要被压缩的文件夹（绝对路径）</param>
        /// <param name="rarPath">压缩后的 .rar 的存放目录（绝对路径）</param>
        /// <param name="rarName">压缩文件的名称（包括后缀）</param>
        /// <returns>true 或 false。压缩成功返回 true，反之，false。</returns>
        public static bool RAR(string path, string rarPath, string rarName)
        {
            bool flag = false;
            string rarexe;       //WinRAR.exe 的完整路径
            RegistryKey regkey;  //注册表键
            Object regvalue;     //键值
            string cmd;          //WinRAR 命令参数
            ProcessStartInfo startinfo;
            Process process;
            try
            {
                regkey = Registry.ClassesRoot.OpenSubKey(@"Applications\WinRAR.exe\shell\open\command");
                regvalue = regkey.GetValue("");  // 键值为 "d:\Program Files\WinRAR\WinRAR.exe" "%1"
                rarexe = regvalue.ToString();
                regkey.Close();
                rarexe = rarexe.Substring(1, rarexe.Length - 7);  // d:\Program Files\WinRAR\WinRAR.exe

                Directory.CreateDirectory(path);
                path = String.Format("\"{0}\"", path);
                //压缩命令，相当于在要压缩的文件夹(path)上点右键->WinRAR->添加到压缩文件->输入压缩文件名(rarName)
                cmd = string.Format("a {0} {1} -ep1 -o+ -inul -r -ibck",
                                    rarName,
                                    path);
                startinfo = new ProcessStartInfo();
                startinfo.FileName = rarexe;
                startinfo.Arguments = cmd;                          //设置命令参数
                startinfo.WindowStyle = ProcessWindowStyle.Hidden;  //隐藏 WinRAR 窗口

                startinfo.WorkingDirectory = rarPath;
                process = new Process();
                process.StartInfo = startinfo;
                process.Start();
                process.WaitForExit(); //无限期等待进程 winrar.exe 退出
                if (process.HasExited)
                {
                    flag = true;
                }
                process.Close();
            }
            catch (Exception e)
            {
                throw new Exception("", e);
            }
            return flag;
        }
        /// <summary>
        /// 利用 WinRAR 进行解压缩
        /// </summary>
        /// <param name="path">文件解压路径（绝对）</param>
        /// <param name="rarPath">将要解压缩的 .rar 文件的存放目录（绝对路径）</param>
        /// <param name="rarName">将要解压缩的 .rar 文件名（包括后缀）</param>
        /// <returns>true 或 false。解压缩成功返回 true，反之，false。</returns>
        public static bool UnRAR(string path, string rarPath, string rarName)
        {
            bool flag = false;
            string rarexe;
            RegistryKey regkey;
            Object regvalue;
            string cmd;
            ProcessStartInfo startinfo;
            Process process;
            try
            {
                regkey = Registry.ClassesRoot.OpenSubKey(@"WinRAR\shell\open\command");
                regvalue = regkey.GetValue("");
                rarexe = regvalue.ToString();
                regkey.Close();
                rarexe = rarexe.Substring(1, rarexe.Length - 7);

                Directory.CreateDirectory(path);
                //解压缩命令，相当于在要压缩文件(rarName)上点右键->WinRAR->解压到当前文件夹
                cmd = string.Format("e {0} {1} -y",
                                    rarName,
                                    path);
                startinfo = new ProcessStartInfo();
                startinfo.FileName = rarexe;
                startinfo.Arguments = cmd;
                startinfo.WindowStyle = ProcessWindowStyle.Hidden;

                startinfo.WorkingDirectory = rarPath;
                process = new Process();
                process.StartInfo = startinfo;
                process.Start();
                process.WaitForExit();
                if (process.HasExited)
                {
                    flag = true;
                }
                process.Close();
            }
            catch (Exception e)
            {
                throw new Exception("", e);
            }
            return flag;
        }

        /// <summary>
        /// Depth-first recursive delete, with handling for descendant 
        /// directories open in Windows Explorer.
        /// </summary>
        public static void ForceDeleteDirectory(DirectoryInfo dir)
        {
            /*
            Process proc = new Process();
            proc.StartInfo.FileName = "CMD.exe";
            proc.StartInfo.Arguments = "/c rmdir /s /q " + path;
            proc.StartInfo.WindowStyle = ProcessWindowStyle.Hidden;
            proc.Start();
            proc.WaitForExit();
            */
            //DirectorySetNormalAttributes(path);
            for (int attempts = 0; attempts < 10; attempts++)
            {
                try
                {
                    if (dir.Exists)
                    {
                        dir.Delete(true);
                    }
                    return;
                }
                catch (Exception ex)
                {
                    System.Windows.MessageBox.Show(ex.Message);
                    GC.Collect();
                    GC.WaitForPendingFinalizers();
                    Thread.Sleep(100);
                    //throw new Exception(e.Message);
                }
            }
        }

        public static void RebuildSubDirTemp()
        {
            if (CurrentContext == null) return;
            string _strcoverspathtemp = Path.Combine(CurrentContext.DirAppTemp.FullName, "_covers");
            if (Directory.Exists(_strcoverspathtemp)) Directory.Delete(_strcoverspathtemp, true);
            GC.Collect();
            GC.WaitForPendingFinalizers();
            CurrentContext.DirCoversTemp = CurrentContext.DirAppTemp.CreateSubdirectory("_covers");

            string _strstarspathtemp = Path.Combine(CurrentContext.DirAppTemp.FullName, "_stars");
            if (Directory.Exists(_strstarspathtemp)) Directory.Delete(_strstarspathtemp, true);
            GC.Collect();
            GC.WaitForPendingFinalizers();
            CurrentContext.DirStarsTemp = CurrentContext.DirAppTemp.CreateSubdirectory("_stars");

            string _strsamplespathtemp = Path.Combine(CurrentContext.DirAppTemp.FullName, "_samples");
            if (Directory.Exists(_strsamplespathtemp)) Directory.Delete(_strsamplespathtemp, true);
            GC.Collect();
            GC.WaitForPendingFinalizers();
            CurrentContext.DirSamplesTemp = CurrentContext.DirAppTemp.CreateSubdirectory("_samples");
        }

        public static BitmapImage LoadBitmapImage(string fileName)
        {
            using (var stream = new FileStream(fileName, FileMode.Open))
            {
                var bitmapImage = new BitmapImage();
                bitmapImage.BeginInit();
                bitmapImage.CacheOption = BitmapCacheOption.OnLoad;
                bitmapImage.StreamSource = stream;
                bitmapImage.EndInit();
                //bitmapImage.Freeze(); // just in case you want to load the image in another thread
                return bitmapImage;
            }
        }

        #endregion
    }

    [Serializable]
    public class XContext
    {
        public int StorageCount { get; set; }
        public List<StarInfo> TotalStars { get; set; }
        public List<MovieInfo> TotalMovies { get; set; }

        public DirectoryInfo DirAppTemp { get; set; }
        public DirectoryInfo DirCoversTemp { get; set; }
        public DirectoryInfo DirStarsTemp { get; set; }
        public DirectoryInfo DirSamplesTemp { get; set; }

        

        public string USite { get; set; } = "www.avsox.com";
        public string CSite { get; set; } = "www.avmoo.com";
        public string BSite { get; set; } = "www.javbus.com";

        //public HttpClient GlobalHttpClient { get; set; }
        //public CancellationTokenSource GlobalCTS { get; set; } = new CancellationTokenSource();

        public bool IsUseProxy { get; set; }
        public IWebProxy SSProxy { get; set; }

        public XContext()
        {
            TotalStars = new List<StarInfo>();
            TotalMovies = new List<MovieInfo>();

            CSite = "avio.pw"; //http://jav.tellme.pw
            USite = "avso.pw"; //http://javu.tellme.pw
            BSite = "www.javbus.com";

            string _strapppathtemp = Path.Combine(Path.GetTempPath(), "__xdatatemp");
            if (Directory.Exists(_strapppathtemp)) XGlobal.ForceDeleteDirectory(new DirectoryInfo(_strapppathtemp));
            DirAppTemp = new DirectoryInfo(Path.GetTempPath()).CreateSubdirectory("__xdatatemp");

            XGlobal.RebuildSubDirTemp();

            IsUseProxy = false;
            SSProxy = new WebProxy("http://127.0.0.1:10800");
        }

        public void ResetWWW()
        {
            CSite = "avio.pw"; //http://jav.tellme.pw
            USite = "avso.pw"; //http://javu.tellme.pw
            BSite = "www.javbus.com";
        }

        public StarInfo GetStarbyUID(Guid uid)
        {
            return TotalStars.Find(s => s.UniqueID == uid);
        }
        public MovieInfo GetMoviebyReleaseID(string rid)
        {
            return TotalMovies.Find(m => m.ReleaseID == rid);
        }

        public bool RemoveMovie(MovieInfo m)
        {
            
            return TotalMovies.Remove(m);
        }
    }

    [Serializable]
    public enum XOfficialSite
    {
        SCUTE = 1,
        GAREA = 2
    };

    public class UIConverter : IValueConverter
    {
        object IValueConverter.Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            try
            {
                // May want to validate url for image types that are supported in Silverlight.
                // if (!ImageValidator.IsImageFileNameSupported((string)value)) return null;
                //return new BitmapImage(new Uri((string)value, UriKind.RelativeOrAbsolute));
                //return new BitmapImage(value as Uri);
                if ((value as Uri).IsFile) return XGlobal.LoadBitmapImage((value as Uri).LocalPath);
                else return new BitmapImage(value as Uri);
            }
            catch { }
            return null;
        }

        object IValueConverter.ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return null;
        }
    }


}
