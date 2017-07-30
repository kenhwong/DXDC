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
            else if (msize >= 1024 * 1024 * 1024) //��С���ڻ����1024M
            {
                return string.Format("{0:0.00} G", (double)msize / (1024 * 1024 * 1024));
            }
            else if (msize >= 1024 * 1024) //��С���ڻ����1024K
            {
                return string.Format("{0:0.00} M", (double)msize / (1024 * 1024));
            }
            else if (msize >= 1024) //��С���ڵ���1024
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
        /// �h��Ŀ��µ�����ָ����Y����ҕ�l�ļ�
        /// </summary>
        /// <param name="sourcedi">ԴĿ�</param>
        /// <param name="ext">�ļ���Y��</param>
        public static void DeleteMovies(DirectoryInfo sourcedi, string ext)
        {
            if (sourcedi.Exists)
            {
                List<FileInfo> files = sourcedi.GetFiles("*" + ext, System.IO.SearchOption.TopDirectoryOnly).Where(f => f.Name.EndsWith(ext, StringComparison.CurrentCultureIgnoreCase)).ToList();
                files.ForEach(c => c.Delete());
            }
            else
            {
                throw new DirectoryNotFoundException(String.Format("ԴĿ¼{0}�����ڣ������ѱ��h����", sourcedi.FullName));
            }
        }

        /// <summary>
        /// �ƶ��ļ����е������ļ������ļ�����һ���ļ���
        /// </summary>
        /// <param name="sourcePath">Դ�ļ���</param>
        /// <param name="destPath">Ŀ���ļ���</param>
        public static void MoveFolder(DirectoryInfo sourcePath, DirectoryInfo destPath)
        {
            if (sourcePath.Exists)
            {
                if (!destPath.Exists)
                {
                    //Ŀ��Ŀ¼�������򴴽�
                    try
                    {
                        destPath.Create();
                    }
                    catch (Exception ex)
                    {
                        throw new Exception("����Ŀ��Ŀ¼ʧ�ܣ�" + ex.Message);
                    }
                }
                //���Դ�ļ��������ļ�
                List<FileInfo> files = new List<FileInfo>(sourcePath.EnumerateFiles());
                files.ForEach(c =>
                {
                    string destFile = Path.Combine(destPath.FullName, c.Name);
                    //����ģʽ
                    if (File.Exists(destFile))
                    {
                        File.Delete(destFile);
                    }
                    c.MoveTo(destFile);
                });
                //���Դ�ļ�������Ŀ¼�ļ�
                List<DirectoryInfo> folders = new List<DirectoryInfo>(sourcePath.EnumerateDirectories());

                folders.ForEach(c =>
                {
                    string destDir = Path.Combine(destPath.FullName, c.Name);
                    //���õݹ�ķ���ʵ��
                    c.MoveTo(destDir);
                });
            }
            else
            {
                throw new DirectoryNotFoundException("ԴĿ¼�����ڣ�");
            }
        }

        /// <summary>
        /// �ƶ��ļ����е������ļ������ļ�����һ���ļ���
        /// </summary>
        /// <param name="sourcePath">Դ�ļ���</param>
        /// <param name="destPath">Ŀ���ļ���</param>
        public static void MoveImageFolder(string sourcePath, string destPath, string ext)
        {
            if (Directory.Exists(sourcePath))
            {
                if (!Directory.Exists(destPath))
                {
                    //Ŀ��Ŀ¼�������򴴽�
                    try
                    {
                        Directory.CreateDirectory(destPath);
                    }
                    catch (Exception ex)
                    {
                        throw new Exception("����Ŀ��Ŀ¼ʧ�ܣ�" + ex.Message);
                    }
                }
                //���Դ�ļ��������ļ�
                List<string> files = new List<string>(Directory.GetFiles(sourcePath, "*" + ext, System.IO.SearchOption.AllDirectories));
                files.ForEach(c =>
                {
                    string destFile = Path.Combine(new string[] { destPath, Path.GetFileName(c) });
                    //����ģʽ
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
                throw new DirectoryNotFoundException("ԴĿ¼�����ڣ�");
            }
        }
        public static void MoveImageFolder(DirectoryInfo dirsource, DirectoryInfo dirdest, string ext)
        {
            if(dirsource.Exists)
            {
                if(!dirdest.Exists)
                    try { dirdest.Create(); } catch (Exception ex) { throw new Exception("����Ŀ��Ŀ¼ʧ�ܣ�" + ex.Message); }

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
        /// ���� WinRAR ����ѹ��
        /// </summary>
        /// <param name="path">��Ҫ��ѹ�����ļ��У�����·����</param>
        /// <param name="rarPath">ѹ����� .rar �Ĵ��Ŀ¼������·����</param>
        /// <param name="rarName">ѹ���ļ������ƣ�������׺��</param>
        /// <returns>true �� false��ѹ���ɹ����� true����֮��false��</returns>
        public static bool RAR(string path, string rarPath, string rarName)
        {
            bool flag = false;
            string rarexe;       //WinRAR.exe ������·��
            RegistryKey regkey;  //ע����
            Object regvalue;     //��ֵ
            string cmd;          //WinRAR �������
            ProcessStartInfo startinfo;
            Process process;
            try
            {
                regkey = Registry.ClassesRoot.OpenSubKey(@"Applications\WinRAR.exe\shell\open\command");
                regvalue = regkey.GetValue("");  // ��ֵΪ "d:\Program Files\WinRAR\WinRAR.exe" "%1"
                rarexe = regvalue.ToString();
                regkey.Close();
                rarexe = rarexe.Substring(1, rarexe.Length - 7);  // d:\Program Files\WinRAR\WinRAR.exe

                Directory.CreateDirectory(path);
                path = String.Format("\"{0}\"", path);
                //ѹ������൱����Ҫѹ�����ļ���(path)�ϵ��Ҽ�->WinRAR->��ӵ�ѹ���ļ�->����ѹ���ļ���(rarName)
                cmd = string.Format("a {0} {1} -ep1 -o+ -inul -r -ibck",
                                    rarName,
                                    path);
                startinfo = new ProcessStartInfo();
                startinfo.FileName = rarexe;
                startinfo.Arguments = cmd;                          //�����������
                startinfo.WindowStyle = ProcessWindowStyle.Hidden;  //���� WinRAR ����

                startinfo.WorkingDirectory = rarPath;
                process = new Process();
                process.StartInfo = startinfo;
                process.Start();
                process.WaitForExit(); //�����ڵȴ����� winrar.exe �˳�
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
        /// ���� WinRAR ���н�ѹ��
        /// </summary>
        /// <param name="path">�ļ���ѹ·�������ԣ�</param>
        /// <param name="rarPath">��Ҫ��ѹ���� .rar �ļ��Ĵ��Ŀ¼������·����</param>
        /// <param name="rarName">��Ҫ��ѹ���� .rar �ļ�����������׺��</param>
        /// <returns>true �� false����ѹ���ɹ����� true����֮��false��</returns>
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
                //��ѹ������൱����Ҫѹ���ļ�(rarName)�ϵ��Ҽ�->WinRAR->��ѹ����ǰ�ļ���
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
