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
        //TODO 
        /*
         * ресайз, красота
         * Защита от дурака
         * ридми мд
         * Чо там с тегами
         * Рефактор имен
         * Добавить fixTag
         * Очередь копирования
         */
        string inputpath;
        string outputpath;
        List<string> allfiles;
        volatile bool needSelect;
        public volatile bool stopRequested;

        Task sortingtask;
        string selectedGenre;
        volatile static MediaPlayer player = new MediaPlayer();
        SliderConverter sliderConverter = new SliderConverter(player);
        List<string> genres = new List<string>();
        Button leaveButton;

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
                var butNow = new Button();
                butNow.Content = now;
                butNow.FontSize = 15;

                colorch = !colorch;
                if (colorch)
                    butNow.Background = Brushes.Gray;
                else
                    butNow.Background = Brushes.LightGray;
                butNow.Height = 30;
                //todo set buttons bigger       
                butNow.Click += delegate
                {
                    player.Stop();
                    selectedGenre = now;
                    needSelect = false;
                };
                buttonsList.Items.Add(butNow);
            }
            var delButton = new Button();
            delButton.Content = "delete";
            delButton.Click += delegate
            {
                player.Stop();
                selectedGenre = "delete";
                needSelect = false;
            };
            buttonsList.Items.Add(delButton);


            leaveButton = new Button();
            leaveButton.Content = "leave";
            leaveButton.Click += delegate
            {
                player.Stop();
                selectedGenre = "leave";
                needSelect = false;
            };
            buttonsList.Items.Add(leaveButton);
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
            songsListBox.ItemsSource = allfiles.ConvertAll(o => Path.GetFileName(o));
            songsListBox.UpdateLayout();
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


        private void startButton_Click(object sender, RoutedEventArgs e)
        {
            sortingtask = new Task(delegate
             {
             foreach (var now in allfiles)
             {
                 Dispatcher.Invoke((() =>
                {
                    songsListBox.SelectedIndex = allfiles.IndexOf(now);
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
                 Dispatcher.Invoke((async () =>
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
                             bool move = (bool)moveCB.IsChecked;
                             await Task.Run(delegate
                             {
                             try
                             {

                                 Directory.CreateDirectory(Path.Combine(outputpath, selectedGenre));
                                 string dstPath = Path.Combine(outputpath, selectedGenre, Path.GetFileName(now));
                                 if (!File.Exists(dstPath))
                                 {
                                         //await CopyFileAsync(now, dstPath, (bool)moveCB.IsChecked);
                                         if (move)
                                         File.Move(now, dstPath);
                                     else
                                         File.Copy(now, dstPath);
                                 }
                                     Thread.Sleep(200);
                                 TagLib.File f = TagLib.File.Create(Path.Combine(outputpath, selectedGenre, Path.GetFileName(now)));
                                 f.Tag.Genres = new string[] { selectedGenre };
                                 f.Save();
                                 }

                                 catch (Exception ex)
                                 {
                                     MessageBox.Show(ex.ToString());
                                 }
                             });
                     }
                 }
                     }));
        };
    });
            stopRequested = false;
            sortingtask.Start();
        }

public async Task CopyFileAsync(string sourcePath, string destinationPath, bool move)
{
    using (Stream source = File.Open(sourcePath, FileMode.Open))
    {
        using (Stream destination = File.Create(destinationPath))
        {
            await source.CopyToAsync(destination);
        }
    }
    if (move)
        await Task.Run(delegate { File.Delete(sourcePath); });

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
        leaveButton.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
}

private void moveCB_Click(object sender, RoutedEventArgs e)
{
    Configuration configuration = ConfigurationManager.OpenExeConfiguration(ConfigurationUserLevel.None);
    configuration.AppSettings.Settings["move"].Value = (Convert.ToInt32(moveCB.IsChecked)).ToString();
    configuration.Save();
}

private void buttonsList_SelectionChanged(object sender, SelectionChangedEventArgs e)
{

}
    }
}
