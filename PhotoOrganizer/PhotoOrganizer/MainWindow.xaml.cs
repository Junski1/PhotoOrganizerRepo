using System;
using System.Collections;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Runtime.Versioning;
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
using static System.Net.Mime.MediaTypeNames;
using Image = System.Drawing.Image;
using GoogleMaps.LocationServices;
using static System.Net.WebRequestMethods;
using File = System.IO.File;
using Microsoft.Graph.Models;
using System.Collections.Concurrent;


namespace PhotoOrganizer
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    [SupportedOSPlatform("windows")]
    public partial class MainWindow : Window
    {
        class ImageInfo
        {
            public FileInfo File;
            public string Date = string.Empty;
            public string Lat = string.Empty;
            public string Long = string.Empty;
            public string Manufactorer = string.Empty;
            public string Model = string.Empty;
        }

        public MainWindow()
        {
            InitializeComponent();
        }

        #region UIEvents
        public void SetLocationTxt(string _path) => LocationTxt.Text = _path;

        private void ProgressBar_ValueChanged(object _sender, RoutedPropertyChangedEventArgs<double> _e) => ProgressTxt.Content = $"Process: {(_sender as ProgressBar).Value} / {(_sender as ProgressBar).Maximum}";

        private void StartBtn_Click(object sender, RoutedEventArgs e) => StartProcess(LocationTxt.Text, GetListBoxAsString(ExtensionList.SelectedItems));

        private void SearchPathBtn_Click(object _sender, RoutedEventArgs _e)
        {
            Window1 _FolderWindow = new Window1(this);
            _FolderWindow.Show();
        }
        #endregion

        DirectoryInfo dir = new DirectoryInfo(Directory.GetCurrentDirectory());

        private async void StartProcess(string _dirPath, string[] _extensions)
        {
            StartBtn.IsEnabled = false;
            ConsoleBox.Text = string.Empty;

            if (_extensions.Length <= 0)
            {
                MessageBox.Show($"{_extensions.Length} Extension were selected!");
                return;
            }

            if (string.IsNullOrEmpty(_dirPath))
            {
                MessageBox.Show($"no directory was selected. Please select a location!");
                return;
            }

            List<ImageInfo> _images = new List<ImageInfo>();
            List<ImageInfo> _removableImgs = new List<ImageInfo>();

            _images = await GetFiles(_dirPath, _extensions);

            ProcessFiles(_images);

            if(RenameCheck.IsChecked.GetValueOrDefault())
               RenameFiles(_images);

            StartBtn.IsEnabled = true;
        }

        #region GetFiles
        private async Task<List<ImageInfo>> GetFiles(string _dirPath, string[] _extensions)
        {
            dir = new DirectoryInfo($@"{_dirPath}\");

            ConcurrentBag<ImageInfo> _files = new ConcurrentBag<ImageInfo>();

            //uses parallel to access multiple treads to utalize all of CPU
            await Parallel.ForEachAsync(dir.GetFilesByExtensions(_extensions), async (_info, token) => 
            {
                try
                {
                    using (Image _tempImg = new Bitmap(_info.FullName))
                    {
                        _files.Add
                        (
                            new ImageInfo
                            {
                                /*  
                                 * 0x9003 = date
                                 * 0x0002 = Lat
                                 * 0x0004 = Long
                                 * 0x010F = manufactorer
                                 * 0x0110 = model
                                 */

                                File = _info,
                                Date = await CheckPropertyId(_tempImg, 0x9003),
                                Lat = await CheckPropertyId(_tempImg, 0x0002, true),
                                Long = await CheckPropertyId(_tempImg, 0x0004, true),
                                Manufactorer = await CheckPropertyId(_tempImg, 0x010F),
                                Model = (await CheckPropertyId(_tempImg, 0x0110)).Replace(' ', '_')
                            }
                            );
                    }
                }
                catch (Exception _e)
                {
                    _files.Add
                   (
                       new ImageInfo
                       {
                           /*  
                            * 0x9003 = date
                            * 0x0002 = Lat
                            * 0x0004 = Long
                            * 0x010F = manufactorer
                            * 0x0110 = model
                            */

                           File = _info,
                           Date = _info.CreationTime.ToString(),
                           Manufactorer = "Video",
                           Model = "Video"
                       }
                   );
                }
                //ProgBar.Maximum = _files.Count;
            });

            ConsoleBox.Text += $@"{Environment.NewLine}{_files.Count} files found with ({string.Concat(_extensions).Replace(".", " .")}) Extensions in {_dirPath}\ directory!{Environment.NewLine}{Environment.NewLine}";

            return _files.ToList();
        }

        private async Task<string> CheckPropertyId(Image _img, Int32 _id, bool _isBit = false)
        {
            string _result = string.Empty;

            foreach (int _PropId in _img.PropertyIdList)
            {
                if (_PropId == _id)
                {
                    if (_isBit)
                        _result = await BitConvertLatLong(_img.GetPropertyItem(_id));
                    else
                        _result = await EncodingToString(_img.GetPropertyItem(_id));

                    break;
                }
            }
            return _result.Trim('\0');
        }

        private async Task<string> BitConvertLatLong(PropertyItem _item)
        {
            //degrees  

            double _d = BitConverter.ToUInt32(_item.Value, 0) * 1.0d / BitConverter.ToUInt32(_item.Value, 4);

            //minutes

            double _m = BitConverter.ToUInt32(_item.Value, 8) * 1.0d / BitConverter.ToUInt32(_item.Value, 12);

            //seconds 

            var _s1 = BitConverter.ToUInt32(_item.Value, 16);

            var _s2 = BitConverter.ToUInt32(_item.Value, 20);

            double _s = (double)_s1 / (double)_s2;

            double _dblGPSLatitude = (((_s / 60 + _m) / 60) + _d);
            return _dblGPSLatitude.ToString();
        }

        private async Task<string> EncodingToString(PropertyItem _item)
        {
            ASCIIEncoding _encoding = new ASCIIEncoding();

            return _encoding.GetString(_item.Value);
        }

        private string[] GetListBoxAsString(IList _list)
        {
            List<string> _elems = new List<string>();

            foreach (var _item in _list)
            {
                string[] _tags = (_item as ListBoxItem)?.Tag.ToString().Split('/');

                foreach (string _tag in _tags)
                {
                    _elems.Add(_tag.ToLower());
                    _elems.Add(_tag.ToUpper());
                }
            }

            return _elems.ToArray<string>();
        }
        #endregion

        #region ProcessFiles
        private void ProcessFiles(List<ImageInfo> _files)
        {
            DateTime _curDate = DateTime.Now;

            foreach (ImageInfo _imgInfo in _files)
            {
                string _curDir = dir.FullName;

                try
                {
                    //creates a directory for the year the photo is taken
                    _curDate = DateTime.Parse(_imgInfo.Date);

                    CreateDir(ref _curDir, _curDate.Year.ToString());

                    /*
                     * Uncomment this code to get Lat and Long converted to Andress.
                     * 
                     * Requires a APIKey from google! Can be acquired for free for 1 year, after that it has to be paid for
                     * Not used in personal use
                     * 
                     * 
                    GoogleLocationService _loc = new GoogleLocationService();
                    AddressData _address = null;

                    ConsoleBox.Text += $"{_imgInfo.Lat}, {_imgInfo.Long}{Environment.NewLine}";
                    if (double.TryParse(_imgInfo.Lat, out double _lat) && double.TryParse(_imgInfo.Long, out double _long))
                        _address = _loc.GetAddressFromLatLang(double.Parse(_imgInfo.Lat), double.Parse(_imgInfo.Long));

                    if (_address != null)
                    {
                        
                        CreateDir(ref _curDir, _address.Country);
                        CreateDir(ref _curDir, _address.City);
                    }
                    */


                    //creates a directory for the day and month the photo is taken
                    CreateDir(ref _curDir, $@"{_curDate.Day}_{_curDate.Month}");

                    _curDir += _imgInfo.File.Name;

                    //if file is already in the right place dont move it
                    if (_imgInfo.File.FullName == _curDir)
                    {
                        ProgBar.Value++;
                        continue;
                    }

                    //TODO: Duplicate check / Create CheckForDups checkbox

                    //if(CheckForDups.IsChecked.GetValueOrDefault())
                    //{
                    //if (File.Exists(_curDir))
                    //{
                    //    
                    //    removableImgs.Add(_imgInfo);
                    //    _imgInfo.File.Delete();
                    //    ConsoleBox.Text += $"{_imgInfo.File.Name} duplicate has been deleted.{Environment.NewLine}";
                    //    ProgBar.Value++;
                    //    continue;
                    //}
                    //}

                    _imgInfo.File.MoveTo(_curDir);

                    ProgBar.Value++;
                }
                catch (Exception _e)
                {
                    ConsoleBox.Text += $"{_e.Message}. on line {_e.Source}.{Environment.NewLine}";
                }
            }

            //remove all deleted flies from images
            //_files.RemoveAll(_item => _item == removableImgs.Find(_rItem => _rItem == _item));
            ConsoleBox.Text += $@"{_files.Count} files moved / deleted!{Environment.NewLine}";
        }

        private void RenameFiles(List<ImageInfo> _files)
        {
            ProgBar.Maximum = _files.Count;
            ProgBar.Value = 0;

            int _count = 0;
            foreach (ImageInfo _info in _files)
            {
                if (_count + 1 > Directory.GetFiles(_info.File.DirectoryName).Length)
                    _count = 0;

                _count++;

                string _modelStr = _info.Model.Contains(_info.Manufactorer) ? _info.Model : $@"{_info.Manufactorer}_{_info.Model}";

                string _newDir = $@"{_info.File.DirectoryName}\{_modelStr}_{_count}{_info.File.Extension}";

                if (!File.Exists(_newDir))
                    _info.File.MoveTo(_newDir);

                ProgBar.Value++;
            }

            ConsoleBox.Text += $@"{_files.Count} files renamed!{Environment.NewLine}";
        }
        private void CreateDir(ref string _curDir, string _nextDir)
        {
            _curDir += $@"{_nextDir}\";

            if (!Directory.Exists(_curDir))
            {
                Directory.CreateDirectory(_curDir);
                ConsoleBox.Text += $"{_nextDir} folder created.{Environment.NewLine}";
            }
        }
        #endregion
    }

    static public class Extensions
    {
        //Extension method to DirectoryInfo
        public static IEnumerable<FileInfo> GetFilesByExtensions(this DirectoryInfo _dir, params string[] _extensions)
        {
            if (_extensions == null)
                throw new ArgumentNullException("extensions");

            //search all files with Extensions
            IEnumerable<FileInfo> files = _dir.EnumerateFiles("*", SearchOption.AllDirectories);
            return files.Where(f => _extensions.Contains(f.Extension));
        }
    }
}
