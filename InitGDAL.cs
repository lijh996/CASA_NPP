using OSGeo.GDAL;
using OSGeo.OGR;
using System;
using System.IO;
using System.Runtime.InteropServices;

namespace VW.RS.Utility
{
    public class InitGDAL
    {
        static InitGDAL()
        {
            Instance = new InitGDAL();
        }
        private InitGDAL()
        {

        }

        public static InitGDAL Instance { get; private set; } = null;

        [DllImport("gdal202.dll", EntryPoint = "OGR_F_GetFieldAsString", CallingConvention = CallingConvention.Cdecl)]
        private extern static IntPtr OGR_F_GetFieldAsString(HandleRef handle, int index);

        public void Init()
        {
            Gdal.SetConfigOption("GDAL_FILENAME_IS_UTF8", "NO");//解决中文乱码问题
            Gdal.SetConfigOption("SHAPE_ENCODING", "");
            Gdal.SetConfigOption("CHECK_DISK_FREE_SPACE", "FALSE");
            //Gdal.SetConfigOption("GDAL_DATA", Path.Combine(AppContext.BaseDirectory,"gata-data"));
            Gdal.AllRegister();
            Ogr.RegisterAll();
        }

        public string GetAnsiFieldValue(Feature feature, int fid)
        {
            IntPtr pStr = OGR_F_GetFieldAsString(Feature.getCPtr(feature), fid);
            return Marshal.PtrToStringAnsi(pStr);
        }
    }
}