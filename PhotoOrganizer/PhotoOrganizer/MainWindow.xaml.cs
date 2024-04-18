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
            public string Date;
            public string Lat;
            public string Long;
            public string Manufactorer;
            public string Model;
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

        private void StartProcess(string _dirPath, string[] _extensions)
        {
            Console.Text = string.Empty;

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

            DirectoryInfo _dir = new DirectoryInfo($@"{_dirPath}\");

            List<ImageInfo> _images = new List<ImageInfo>();
            List<ImageInfo> _removableImgs = new List<ImageInfo>();

            foreach (FileInfo _info in _dir.GetFilesByExtensions(_extensions))
            {
                Image _tempImg = new Bitmap(_info.FullName);

                try
                {
                    _images.Add
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
                            Date = CheckPropertyId(_tempImg, 0x9003),
                            Lat = CheckPropertyId(_tempImg, 0x0002, true),
                            Long = CheckPropertyId(_tempImg, 0x0004, true),
                            Manufactorer = CheckPropertyId(_tempImg, 0x010F),
                            Model = CheckPropertyId(_tempImg, 0x0110).Replace(' ', '_')
                        }
                    );
                }
                catch (Exception _e) { MessageBox.Show(_e.ToString()); }

                _tempImg.Dispose();
            }


            Console.Text += $@"{_images.Count} files found with ({string.Concat(_extensions).Replace(".", " .")}) Extensions in {_dirPath}\ directory!{Environment.NewLine}";
            //MessageBox.Show($"{_images.Count} files were found with ({_extensions}) Extensions in @{_dirPath} directory!");
            ProgBar.Maximum = _images.Count;
            ProgBar.Value = 0;

            DateTime _curDate = DateTime.Now;

            foreach (ImageInfo _imgInfo in _images)
            {
                string _curDir = _dir.FullName;

                try
                {
                    //here create folder for the place photo is taken

                    //string _Lat = _imgInfo.Lat;
                    //string _Long = _imgInfo.Long;

                    //creates a directory for the year the photo is taken
                    _curDate = DateTime.Parse(_imgInfo.Date);

                    CreateDir(ref _curDir, _curDate.Year.ToString());

                    //creates a directory for the day and month the photo is taken
                    CreateDir(ref _curDir, $@"{_curDate.Day}_{_curDate.Month}");

                    _curDir += _imgInfo.File.Name;

                    //if file is already in the right place dont move it
                    if (_imgInfo.File.FullName == _curDir)
                    {
                        ProgBar.Value++;
                        continue;
                    }

                    //if file already exist delete duplicate
                    if (File.Exists(_curDir))
                    {
                        Console.Text += $"{_imgInfo.File.Name} duplicate has been deleted.{Environment.NewLine}";

                        _removableImgs.Add(_imgInfo);
                        _imgInfo.File.Delete();

                        ProgBar.Value++;
                        continue;
                    }

                    _imgInfo.File.MoveTo(_curDir);

                    ProgBar.Value++;
                }
                catch (Exception _e)
                {
                    Console.Text += $"{_e.Message}. on line {_e.Source}.{Environment.NewLine}";
                }
            }

            //remove all deleted flies from images
            _images.RemoveAll(_item => _item == _removableImgs.Find(_rItem => _rItem == _item));

            RenameFiles(_images);
        }

        #region GetFiles
        private string CheckPropertyId(Image _img, Int32 _id, bool _isBit = false)
        {
            string _result = string.Empty;

            foreach (int _PropId in _img.PropertyIdList)
            {
                if (_PropId == _id)
                {
                    if (_isBit)
                        _result = BitConvertLatLong(_img.GetPropertyItem(_id));
                    else
                        _result = EncodingToString(_img.GetPropertyItem(_id));

                    break;
                }
            }
            return _result.Trim('\0');
        }

        private string BitConvertLatLong(PropertyItem _item)
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

        private string EncodingToString(PropertyItem _item)
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
        }
        private void CreateDir(ref string _curDir, string _nextDir)
        {
            _curDir += $@"{_nextDir}\";

            if (!Directory.Exists(_curDir))
            {
                Directory.CreateDirectory(_curDir);
                Console.Text += $"{_nextDir} folder created.{Environment.NewLine}";
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
