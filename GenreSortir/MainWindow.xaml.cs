using System;
using System.Collections.Generic;
using System.Configuration;
using System.IO;
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
using System.ComponentModel;
using System.Threading;
//using System.Windows.Shapes;

namespace GenreSortir
{
    /// <summary>
    /// Логика взаимодействия для MainWindow.xaml
    /// </summary>
    /// 

    public class SliderConverter : INotifyPropertyChanged
    {
        private double position = 0;
        private MediaPlayer player = null;


        public SliderConverter(MediaPlayer pRef)
        {
            this.player = pRef;
        }

        public double Position
        {
            get
            {
                return position;
            }
            set
            {
                position = value;
                player.Position = new TimeSpan(0, 0, (int)position);
                OnPropertyChanged("Position");
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;
        private void OnPropertyChanged(string info)
        {
            PropertyChangedEventHandler handler = PropertyChanged;
            if (handler != null)
            {
                handler(this, new PropertyChangedEventArgs(info));
            }
        }
    }
    public partial class MainWindow : Window
    {
        string inputpath;
        string outputpath;
        List<string> allfiles;
        volatile bool needSelect;
        public volatile bool stopRequested;
        Button butt2;
        string selectedGenre;
        volatile static MediaPlayer player = new MediaPlayer();
        SliderConverter sliderConverter = new SliderConverter(player);
        List<string> genres = new List<string>();
        public MainWindow()
        {
            InitializeComponent();
            ChangeInputFolder(ConfigurationManager.AppSettings["inputpath"]);
            outputpath = ConfigurationManager.AppSettings["outputpath"];
            moveCB.IsChecked = Convert.ToBoolean(int.Parse(ConfigurationManager.AppSettings["move"]));
            textBox_Copy.Text = outputpath;
            slider.DataContext = sliderConverter;
            genres = ConfigurationManager.AppSettings["categories"].Split(' ').ToList();
            bool colorch = false;
            foreach (var now in genres)
            {
                var but = new Button();
                but.Content = now;
                but.FontSize = 15;
                colorch = !colorch;
                if (colorch)
                    but.Background = Brushes.Gray;
                else
                    but.Background = Brushes.LightGray;
                but.Width = 500;
                but.Height = 30;
                //todo set buttons bigger       
                but.Click += delegate
                {
                    player.Stop();
                    selectedGenre = now;
                    needSelect = false;
                };
                buttonsList.Items.Add(but);
            }
            var butt = new Button();
            butt.Content = "delete";
            butt.Click += delegate
            {
                player.Stop();
                selectedGenre = "delete";
                needSelect = false;
            };
            buttonsList.Items.Add(butt);

            butt2 = new Button();
            butt2.Content = "leave";
            butt2.Click += delegate
            {
                player.Stop();
                selectedGenre = "leave";
                needSelect = false;
            };
            buttonsList.Items.Add(butt2);


            player.MediaFailed += med;
        }
        static EventHandler<System.Windows.Media.ExceptionEventArgs> med = delegate
        {
            player = new MediaPlayer();
            player.MediaFailed += med;
        };


        private bool ChangeInputFolder(string folderpath)
        {
            stopRequested = true;
            if (!Directory.Exists(folderpath))
                return false;
            inputpath = folderpath;
            Configuration configuration = ConfigurationManager.OpenExeConfiguration(ConfigurationUserLevel.None);
            configuration.AppSettings.Settings["inputpath"].Value = folderpath;
            configuration.Save();
            textBox.Text = folderpath;
            allfiles = Directory.GetFiles(folderpath, "*.mp3", SearchOption.AllDirectories).ToList();
            listBox.ItemsSource = allfiles.ConvertAll(o => Path.GetFileName(o));
            return true;
        }

        private void button_Click(object sender, RoutedEventArgs e)
        {
            System.Windows.Forms.FolderBrowserDialog folderBrowserDialog1 = new System.Windows.Forms.FolderBrowserDialog();
            if (folderBrowserDialog1.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {
                ChangeInputFolder(folderBrowserDialog1.SelectedPath);
            }
        }
        Task sortingtask;
        private void startButton_Click(object sender, RoutedEventArgs e)
        {
            sortingtask = new Task(delegate
             {
                 foreach (var now in allfiles)
                 {
                     Dispatcher.Invoke((() =>
                    {
                        listBox.SelectedIndex = allfiles.IndexOf(now);
                        label1.Content = Path.GetFileName(now);
                        player.Open(new Uri(now, UriKind.Relative));
                        player.Play();
                        player.Position = new TimeSpan(0, 0, 10);

                    }));

                     needSelect = true;
                     while (needSelect)
                     {
                         if (stopRequested)
                             break;
                         Thread.Sleep(100);
                     }
                     if (stopRequested)
                         break;
                     Dispatcher.Invoke((() =>
                     {
                         player.Stop();
                         player.Close();
                         Thread.Sleep(200);
                         if (selectedGenre == "leave")
                         { }
                         else
                         {
                             if (selectedGenre == "delete")
                                 File.Delete(now);
                             else
                             {
                                 try
                                 {

                                     Directory.CreateDirectory(Path.Combine(outputpath, selectedGenre));
                                     if (!File.Exists(Path.Combine(outputpath, selectedGenre, Path.GetFileName(now))))
                                     {
                                         if ((bool)moveCB.IsChecked)
                                             File.Move(now, Path.Combine(outputpath, selectedGenre, Path.GetFileName(now)));
                                         else
                                             File.Copy(now, Path.Combine(outputpath, selectedGenre, Path.GetFileName(now)));
                                     }
                                     TagLib.File f = TagLib.File.Create(Path.Combine(outputpath, selectedGenre, Path.GetFileName(now)));
                                     f.Tag.Genres = new string[] { selectedGenre };
                                     f.Save();
                                 }
                                 catch
                                 {
                                     MessageBox.Show("Some shit happend");
                                 }
                             }
                         }
                     }));
                 };
             });
            stopRequested = false;
            sortingtask.Start();
        }

        private void button_Copy_Click(object sender, RoutedEventArgs e)
        {
            System.Windows.Forms.FolderBrowserDialog folderBrowserDialog1 = new System.Windows.Forms.FolderBrowserDialog();
            if (folderBrowserDialog1.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {
                outputpath = folderBrowserDialog1.SelectedPath;
                Configuration configuration = ConfigurationManager.OpenExeConfiguration(ConfigurationUserLevel.None);
                configuration.AppSettings.Settings["outputpath"].Value = outputpath;
                configuration.Save();
                textBox_Copy.Text = outputpath;
            }
        }

        private void Window_KeyUp(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.N)
                butt2.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        }

        private void moveCB_Click(object sender, RoutedEventArgs e)
        {
            Configuration configuration = ConfigurationManager.OpenExeConfiguration(ConfigurationUserLevel.None);
            configuration.AppSettings.Settings["move"].Value = (Convert.ToInt32(moveCB.IsChecked)).ToString();
            configuration.Save();
        }
    }
}
