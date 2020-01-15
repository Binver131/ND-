using System;
using System.Collections.Generic;
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
using System.Windows.Shapes;
using System.Diagnostics;
using System.Windows.Interop;

using LockheedMartin.Prepar3D.SimConnect;
using System.Runtime.InteropServices;
using System.Threading;

namespace compass
{
    /// <summary>
    /// MainWindow.xaml 的交互逻辑
    /// </summary>
    public partial class MainWindow : Window
    {
        // User-defined win32 event
        const int WM_USER_SIMCONNECT = 0x0402;
        double lastAngle = 0;
        // SimConnect object
        SimConnect simconnect = null;

        enum DEFINITIONS
        {
            Struct1,
        }

        enum DATA_REQUESTS
        {
            REQUEST_1,
        };

        // this is how you declare a data structure so that
        // simconnect knows how to fill it/read it.
        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Ansi, Pack = 1)]
        struct Struct1
        {
            // this is how you declare a fixed size string
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 256)]
            public String title;
            public double latitude;
            public double longitude;
            public double altitude;
            public double TUR;
        };

        public MainWindow()
        {
            InitializeComponent();
           
        }

        #region 消息处理
        protected override void OnSourceInitialized(EventArgs e)
        {
            base.OnSourceInitialized(e);
            HwndSource source = PresentationSource.FromVisual(this) as HwndSource;
            source.AddHook(WndProc);
            try
            {
                // the constructor is similar to SimConnect_Open in the native API
                simconnect = new SimConnect("Managed Data Request", new WindowInteropHelper(this).Handle, WM_USER_SIMCONNECT, null, 0);
                initDataRequest();
            }
            catch (COMException ex)
            {
                Trace.WriteLine("Unable to connect to Prepar3D:\n\n" + ex.Message);
            }
            Thread th = new Thread(requestData);
            if(simconnect != null)
                th.Start();
           

        }

        private void requestData()
        {
             while (true)
            {
                simconnect.RequestDataOnSimObjectType(DATA_REQUESTS.REQUEST_1, DEFINITIONS.Struct1, 0, SIMCONNECT_SIMOBJECT_TYPE.USER);
                Thread.Sleep(200);
            }
            
            
        }

        private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
        {
            if (msg == WM_USER_SIMCONNECT)
            {
                if (simconnect != null)
                {
                    simconnect.ReceiveMessage();
                    handled = true;
                }
            }

            return IntPtr.Zero;
        }

        #endregion


        public TransformGroup SetAngleXY(double Angle)
        {
            TransformGroup tfGroup = new TransformGroup();
            RotateTransform rt = new RotateTransform();
            rt.Angle = Angle;
            rt.CenterX = 0.5;
            rt.CenterY = 0.5;
            tfGroup.Children.Add(rt);
            return tfGroup;
        }

        private void slider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            this.canvas.RenderTransform = SetAngleXY(e.NewValue);

        }

        // Simconnect client will send a win32 message when there is 
        // a packet to process. ReceiveMessage must be called to
        // trigger the events. This model keeps simconnect processing on the main thread.

      

        void simconnect_OnRecvSimobjectDataBytype(SimConnect sender, SIMCONNECT_RECV_SIMOBJECT_DATA_BYTYPE data)
        {

            switch ((DATA_REQUESTS)data.dwRequestID)
            {
                case DATA_REQUESTS.REQUEST_1:
                    Struct1 s1 = (Struct1)data.dwData[0];
                    double angle = s1.TUR * 180 / 3.1415926;
                    Trace.WriteLine("Title: " + s1.title);
                    Trace.WriteLine("Lat:   " + s1.latitude);
                    Trace.WriteLine("Lon:   " + s1.longitude);
                    Trace.WriteLine("Alt:   " + s1.altitude);
                    Trace.WriteLine("TUR:   " + angle);
                    if(Math.Abs(angle - lastAngle) > 0.1)
                    {
                        this.canvas.RenderTransform = SetAngleXY(-angle);
                        this.label_DIRValue.Content = (int)angle;
                        lastAngle = angle;
                    }
                   
                    break;

                default:
                    Trace.WriteLine("Unknown request ID: " + data.dwRequestID);
                    break;
            }
        }

        // Set up all the SimConnect related data definitions and event handlers
        private void initDataRequest()
        {
            try
            {
                // listen to connect and quit msgs
                simconnect.OnRecvOpen += new SimConnect.RecvOpenEventHandler(simconnect_OnRecvOpen);
                simconnect.OnRecvQuit += new SimConnect.RecvQuitEventHandler(simconnect_OnRecvQuit);

                // listen to exceptions
                simconnect.OnRecvException += new SimConnect.RecvExceptionEventHandler(simconnect_OnRecvException);

                // define a data structure
                simconnect.AddToDataDefinition(DEFINITIONS.Struct1, "Title", null, SIMCONNECT_DATATYPE.STRING256, 0.0f, SimConnect.SIMCONNECT_UNUSED);
                simconnect.AddToDataDefinition(DEFINITIONS.Struct1, "Plane Latitude", "degrees", SIMCONNECT_DATATYPE.FLOAT64, 0.0f, SimConnect.SIMCONNECT_UNUSED);
                simconnect.AddToDataDefinition(DEFINITIONS.Struct1, "Plane Longitude", "degrees", SIMCONNECT_DATATYPE.FLOAT64, 0.0f, SimConnect.SIMCONNECT_UNUSED);
                simconnect.AddToDataDefinition(DEFINITIONS.Struct1, "Plane Altitude", "feet", SIMCONNECT_DATATYPE.FLOAT64, 0.0f, SimConnect.SIMCONNECT_UNUSED);
                simconnect.AddToDataDefinition(DEFINITIONS.Struct1, "PLANE HEADING DEGREES MAGNETIC", "Radians", SIMCONNECT_DATATYPE.FLOAT64, 0.0f, SimConnect.SIMCONNECT_UNUSED);
                // IMPORTANT: register it with the simconnect managed wrapper marshaller
                // if you skip this step, you will only receive a uint in the .dwData field.
                simconnect.RegisterDataDefineStruct<Struct1>(DEFINITIONS.Struct1);

                // catch a simobject data request
                simconnect.OnRecvSimobjectDataBytype += new SimConnect.RecvSimobjectDataBytypeEventHandler(simconnect_OnRecvSimobjectDataBytype);
            }
            catch (COMException ex)
            {
                Trace.WriteLine(ex.Message);
            }
        }

        private void simconnect_OnRecvException(SimConnect sender, SIMCONNECT_RECV_EXCEPTION data)
        {
            throw new NotImplementedException();
        }

        private void simconnect_OnRecvQuit(SimConnect sender, SIMCONNECT_RECV data)
        {
            Trace.WriteLine("P3D退出！！\n");
        }

        private void simconnect_OnRecvOpen(SimConnect sender, SIMCONNECT_RECV_OPEN data)
        {
            Trace.WriteLine("连接成功！！\n");
        }
    }
}
