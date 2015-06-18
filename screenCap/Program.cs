using System;
using System.Collections.Generic;
using System.Configuration;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using NReco.VideoConverter;
using MediaInfoDotNet;
using MediaInfoDotNet.Models;

namespace screenCap
{
    class Program
    {
        static void Main(string[] args)
        {
            var pathSetting = ConfigurationManager.AppSettings["TheseFolders"];
            var paths = pathSetting.Split(',');

            foreach (var path in paths)
            {
                Console.WriteLine(path);
                Console.WriteLine(">>>>>>");
                DirSearch(path);
                MoveClean(path);
                Console.WriteLine("-------------");
            }
        }

        static void DirSearch(string sDir)
        {
            var dirs = Directory.GetDirectories(sDir, "*.*", SearchOption.AllDirectories).ToList();
            dirs.Add(sDir);

            var vidExt = ConfigurationManager.AppSettings["VidExtentions"].Split(',').ToArray().Select(x => "." + x.ToString().ToLower());

            try
            {
                foreach (var dir in dirs)
                {
                    var files = Directory.GetFiles(dir)
                        .Select(x => new FileInfo(x))
                        .Where(x => vidExt.Contains(x.Extension))
                        .Select(x => x.FullName);

                    foreach (string f in files)
                    {
                        Console.WriteLine(string.Format("exists {1} - {0}", f, DoesThumbSheetExist(f)));
                        if (!DoesThumbSheetExist(f))
                        {
                            try
                            {
                                var dims = ConfigurationManager.AppSettings["WidthColRow"].Split(',').ToArray();
                                var j = new VidThumb(new FileInfo(f), Convert.ToInt16(dims[0]), Convert.ToInt16(dims[1]), Convert.ToInt16(dims[2]));
                            }
                            catch (Exception ex)
                            {
                                Console.WriteLine("error - " + ex.Message);
                            }
                        }
                        Console.WriteLine("");
                    }
                }
            }
            catch (Exception excpt)
            {
                Console.WriteLine(excpt.Message);
            }
        }

        static void MoveClean(string sDir)
        {
            var copyToPath = sDir.AddEndingSlash();
            var copyFromPath = "";

            var videoName = "";
            var pngName = "";

            var dirs = Directory.GetDirectories(copyToPath, "*.*", SearchOption.AllDirectories)
                .OrderByDescending(x => x.Length);

            var vidExt = ConfigurationManager.AppSettings["VidExtentions"].Split(',').ToArray().Select(x => "." + x.ToString().ToLower());

            foreach (var dir in dirs)
            {
                copyFromPath = dir.AddEndingSlash();

                var files = Directory.GetFiles(dir)
                    .Select(x => new FileInfo(x))
                    .Where(x => vidExt.Contains(x.Extension))
                    .Select(x => x);

                if (files.Count() == 0) {

                    try
                    {
                        foreach (var deleteFile in Directory.GetFiles(dir))
                        {
                            File.Delete(deleteFile);
                        }
                        Directory.Delete(dir, true);
                    }
                    catch (Exception ex) {
                        Console.WriteLine("error: " + ex.Message);
                    }

                    continue;
                }

                foreach (var file in files)
                {
                    videoName = file.Name;
                    pngName = file.Name.Replace(file.Extension, ".png");

                    try
                    {
                        if (!File.Exists(copyToPath + file.Name))
                        {
                            File.Move(copyFromPath + videoName, copyToPath + videoName);
                            File.Move(copyFromPath + pngName, copyToPath + pngName);
                        }
                        Directory.Delete(copyFromPath);
                    }
                    catch (Exception) { }
                }
            }
        }

        static bool DoesThumbSheetExist(string video, bool deleteThumb = false)
        {
            var fileInfo = new FileInfo(video);
            var thumb = string.Format(@"{0}\{1}", fileInfo.Directory, fileInfo.Name.Replace(fileInfo.Extension, ".png"));

            if (deleteThumb)
            {
                File.Delete(thumb);
                return false;
            }
            else
            {
                return File.Exists(thumb);
            }
        }

        public class VidThumb
        {
            public VidThumb(FileInfo mFileInfo, int pSheetWith, int pCols, int pRows)
            {
                this.vidFile = mFileInfo;
                this.mm = new MediaFile(mFileInfo.FullName);
                this.SheetWith = pSheetWith;
                this.Cols = pCols;
                this.Rows = pRows;
                this.SheetSize = new ImageSize();
                this.ThumbSize = new ImageSize();
                this.Thumbs = new Dictionary<string, Image>();
                this.uid = Guid.NewGuid().ToString().Replace("-", "");

                if (this.GenerateThumbnails())
                {
                    this.CreateThumbSheet();
                    this.WriteHeader();
                }
            }

            public FileInfo vidFile { get; set; }
            public MediaFile mm { get; set; }
            public int SheetWith { get; set; }
            public int Cols { get; set; }
            public int Rows { get; set; }
            public ImageSize SheetSize { get; set; }
            public ImageSize ThumbSize { get; set; }
            public Bitmap ThumbSheet { get; set; }
            public Dictionary<string, Image> Thumbs { get; set; }
            public string uid { get; set; }

            private bool GenerateThumbnails()
            {
                if (this.mm.duration < 10000) return false;

                float lenVideo = this.mm.duration > 10000 ? (this.mm.duration / 1000) - 5 : (this.mm.duration / 1000);
                float lenCapLn = lenVideo / (this.Rows * this.Cols);
                float lenStart = 5;
                Dictionary<string, Image> thms = new Dictionary<string, Image>();

                using (var ffmpeg = new FFMpegConverterMine())
                {
                    try
                    {
                        for (int i = 0; i < this.Rows * this.Cols; i++)
                        {
                            var thmName = ThumbFileName(i);

                            using (Stream strm = new MemoryStream())
                            {
                                ffmpeg.GetVideoThumbnail(this.vidFile.FullName, strm, lenStart);

                                var img = Image.FromStream(strm);
                                using (var gph = Graphics.FromImage(img)) {
                                    var fnt = new Font("Courier New", Convert.ToInt32(img.Height * 0.06), FontStyle.Bold);
                                    var str = string.Format("{0:00}:{1:00}:{2:00}", lenStart / 3600, (lenStart / 60) % 60, lenStart % 60);

                                    var strPt = new Point
                                    {
                                        X = img.Width - Convert.ToInt32(fnt.Size * 7),
                                        Y = Convert.ToInt32(img.Height * 0.915)
                                    };

                                    var blackBk = new SolidBrush(Color.Black);
                                    var rect = new Rectangle
                                    {
                                        Height = Convert.ToInt32(img.Height * 0.5),
                                        Width = Convert.ToInt32(img.Width * 0.5),
                                        X = img.Width - Convert.ToInt32(fnt.Size * 7),
                                        Y = Convert.ToInt32(img.Height * 0.913)
                                    };
                                    gph.FillRectangle(blackBk, rect);
                                    gph.DrawString(str, fnt, Brushes.White, strPt);

                                    this.Thumbs.Add(thmName, new Bitmap(img));
                                }
                            }

                            lenStart += lenCapLn;
                        }
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine("err >> " + ex.Message);
                    }
                }
                return true;
            }

            private void CreateThumbSheet()
            {
                var thumbWidth = ((this.SheetWith - 20) - (this.Cols * 10)) / this.Cols;
                var thumbHeightRatio = (thumbWidth * 1.0) / this.mm.Video[0].width;
                var thumbHeight = this.mm.Video[0].height * thumbHeightRatio;

                this.ThumbSize.Height = Convert.ToInt16(thumbHeight);
                this.ThumbSize.Width = thumbWidth;

                this.SheetSize.Height = 55 + ((this.ThumbSize.Height + 10) * this.Rows);
                this.SheetSize.Width = this.SheetWith;

                this.ThumbSheet = new Bitmap(this.SheetSize.Width, this.SheetSize.Height);

                using (var gph = Graphics.FromImage(this.ThumbSheet))
                {
                    gph.Clear(Color.Violet);

                    int i = 0, posX = 15, posY = 50;

                    for (int r = 0; r < this.Rows; r++)
                    {
                        for (int c = 0; c < this.Cols; c++)
                        {
                            var thmName = ThumbFileName(i++);
                            var thmImag = this.Thumbs.Where(x => x.Key == thmName).Select(x => x.Value).FirstOrDefault();
                            using (Image img = (Image)(
                                new Bitmap(thmImag, new Size
                                {
                                    Height = this.ThumbSize.Height,
                                    Width = this.ThumbSize.Width
                                })
                            ))
                            {
                                gph.DrawImage(img, new Point(posX, posY));
                                posX += 10 + this.ThumbSize.Width;
                            }
                        }
                        posX = 15;
                        posY += 10 + this.ThumbSize.Height;
                    }
                }
            }

            private void WriteHeader()
            {
                float billion = 1000000000;
                float million = 1000000;

                string firstText = this.vidFile.Name;
                string secondText = "";

                if (this.mm.size > billion)
                {
                    secondText = Math.Round(this.mm.size / billion, 1) + "GB";
                }
                else if (this.mm.size > million)
                {
                    secondText = Math.Round(this.mm.size / million, 1) + "MB";
                }
                secondText = string.Format("{0}, {1}x{2}, {3}fps", secondText, this.mm.Video[0].width, this.mm.Video[0].height, this.mm.Video[0].frameRate);

                PointF firstLocation = new PointF(10f, 5f);
                PointF secondLocation = new PointF(10f, 25f);
                PointF thirdLocation = new PointF(10f, 45f);

                using (Graphics graphics = Graphics.FromImage(this.ThumbSheet))
                {
                    using (Font arialFont = new Font("Courier New", 12, FontStyle.Bold))
                    {
                        graphics.DrawString(firstText, arialFont, Brushes.Black, firstLocation);
                        graphics.DrawString(secondText, arialFont, Brushes.Black, secondLocation);
                    }
                }

                string thmFile = string.Format(@"{0}\{1}", this.vidFile.Directory, this.vidFile.Name.Replace(this.vidFile.Extension, ".png"));

                this.ThumbSheet.Save(thmFile, ImageFormat.Png);
                Console.WriteLine("created - " + thmFile);
            }

            private string ThumbFileName(int id)
            {
                var fn = string.Format(@"{0}\{2}{1}.jpg", this.vidFile.Directory, (id + 1).ToString("00"), this.uid);
                Console.WriteLine(fn);
                return fn;
            }

            public class ImageSize
            {
                public int Width { get; set; }
                public int Height { get; set; }
            }

            public class FFMpegConverterMine : FFMpegConverter, IDisposable
            {
                public void Dispose()
                {
                }
            }
        }
    }

    public static class StringExtension
    {
        public static string AddEndingSlash(this string path)
        {
            var oijwef = path.EndsWith(@"\") ? path : path += @"\";
            return oijwef;
        }
    }
}
