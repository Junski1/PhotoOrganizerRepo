using System;
using System.Collections.Generic;
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
using System.Windows.Shapes;

namespace PhotoOrganizer
{
    /// <summary>
    /// Interaction logic for Window1.xaml
    /// </summary>
    [SupportedOSPlatform("windows")]
    //TODO: Make Images for Path search
    public partial class Window1 : Window
    {
        Window mainWin = null;

        private object dummyNode = null;
        private string selectedPath = string.Empty;

        public Window1(Window _win)
        {
            InitializeComponent();
            mainWin = _win;
        }

        private void WindowLoaded(object _sender, RoutedEventArgs _e)
        {
            //reset selectedPath when window is opened
            selectedPath = string.Empty;

            //gets drives of the computer
            foreach (string _s in Directory.GetLogicalDrives())
            {
                TreeViewItem _item = new TreeViewItem();

                _item.Header = _s;
                _item.Tag = _s;
                _item.FontWeight = FontWeights.Normal;

                _item.Items.Add(dummyNode);

                _item.Expanded += new RoutedEventHandler(FolderExpanded);
                _item.MouseDoubleClick += new MouseButtonEventHandler(OnDoubleClickFolder);

                FolderItem.Items.Add(_item);
            }
        }

        private void FolderExpanded(object _sender, RoutedEventArgs _e)
        {
            TreeViewItem _item = (TreeViewItem)_sender;
            if (_item.Items.Count == 1 && _item.Items[0] == dummyNode)
            {
                _item.Items.Clear();
                try
                {
                    foreach (string _s in Directory.GetDirectories(_item.Tag.ToString()))
                    {
                        TreeViewItem _subitem = new TreeViewItem();

                        _subitem.Header = _s.Substring(_s.LastIndexOf("\\") + 1);
                        _subitem.Tag = _s;
                        _subitem.FontWeight = FontWeights.Normal;

                        _subitem.Items.Add(dummyNode);

                        _subitem.Expanded += new RoutedEventHandler(FolderExpanded);
                        _subitem.MouseDoubleClick += new MouseButtonEventHandler(OnDoubleClickFolder);

                        _item.Items.Add(_subitem);
                    }
                }
                catch (Exception) { }
            }
        }

        //called when double clicked on folder treeViewItem
        private void OnDoubleClickFolder(object _sender, RoutedEventArgs _e)
        {
            if (!string.IsNullOrEmpty(selectedPath))
                return;

            //if isnt null set tag as path and send it to MainWindow
            selectedPath = (_sender as TreeViewItem).Tag.ToString();
            (mainWin as MainWindow).SetLocationTxt(selectedPath);
            this.Close();
        }
    }
}
