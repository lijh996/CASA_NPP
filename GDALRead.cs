using OSGeo.GDAL;
using System;
using System.Runtime.InteropServices;
using VW.RS.Utility;

namespace VW.RS.IO
{
    public class GDALRead : IDisposable
    {
        /// <summary>
        /// 获取栅格数据的基本信息
        /// </summary>
        /// <param name="fileName">输入数据的全路径</param>
        /// <param name="access">打开方式</param>
        public GDALRead(string fileName, Access access)
        {
            DS = Gdal.Open(fileName, access);

            BandCount = DS.RasterCount;
            Column = DS.RasterXSize;
            Row = DS.RasterYSize;

            double[] transform = new double[6];
            DS.GetGeoTransform(transform);
            Transform = transform;
            XMin = transform[0];
            YMax = transform[3];
            CellSizeX = Math.Round(transform[1], 10);
            CellSizeY = Math.Round(-transform[5], 10);
            XMax = XMin + CellSizeX * Column;
            YMin = YMax - CellSizeY * Row;

            Projection = DS.GetProjectionRef();

            if (BandCount > 0)
            {
                DataType = DS.GetRasterBand(1).DataType;
            }

            int hasval;
            double noDataValue;
            DS.GetRasterBand(1).GetNoDataValue(out noDataValue, out hasval);
            NoDataValue = hasval == 1 ? noDataValue : double.NaN;
        }

        /// <summary>
        /// 数据集对象
        /// </summary>
        public Dataset DS { get; private set; } = null;

        /// <summary>
        /// 关闭数据集
        /// </summary>
        public void Dispose()
        {
            DS.FlushCache();
            DS.Dispose();
        }

        /// <summary>
        /// 波段数
        /// </summary>
        public int BandCount { get; private set; } = 0;

        /// <summary>
        /// 列数
        /// </summary>
        public int Column { get; private set; } = 0;

        /// <summary>
        /// 行数
        /// </summary>
        public int Row { get; private set; } = 0;

        /// <summary>
        /// 转换参数
        /// </summary>
        public double[] Transform { get; private set; } = null;

        /// <summary>
        /// 最小经度
        /// </summary>
        public double XMin { get; private set; } = 0;

        /// <summary>
        /// 最小纬度
        /// </summary>
        public double YMin { get; private set; } = 0;

        /// <summary>
        /// 最大经度
        /// </summary>
        public double XMax { get; private set; } = 0;

        /// <summary>
        /// 最大纬度
        /// </summary>
        public double YMax { get; private set; } = 0;

        /// <summary>
        /// 像元横向分辨率
        /// </summary>
        public double CellSizeX { get; private set; } = 0;

        /// <summary>
        /// 像元纵向分辨率
        /// </summary>
        public double CellSizeY { get; private set; } = 0;

        /// <summary>
        /// 投影信息
        /// </summary>
        public string Projection { get; private set; } = "";

        /// <summary>
        /// 像元类型
        /// </summary>
        public DataType DataType { get; private set; } = DataType.GDT_Unknown;

        /// <summary>
        /// NoDataValue
        /// </summary>
        public double NoDataValue { get; private set; } = -3000;

        public static IntPtr AllocateMemory(DataType dataType, int imageWidth, int imageHeight)
        {
            int bLength;
            switch (dataType)
            {
                case DataType.GDT_Float64:
                    bLength = 8;
                    break;
                case DataType.GDT_Float32:
                case DataType.GDT_UInt32:
                case DataType.GDT_Int32:
                    bLength = 4;
                    break;
                case DataType.GDT_UInt16:
                case DataType.GDT_Int16:
                    bLength = 2;
                    break;
                case DataType.GDT_Byte:
                    bLength = 1;
                    break;
                default:
                    throw new Exception("像元类型不支持：" + dataType.ToString());
            }

            return Marshal.AllocHGlobal(imageWidth * imageHeight * bLength);
        }
    }
}