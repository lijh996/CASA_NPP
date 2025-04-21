using OSGeo.GDAL;
using System;
using System.IO;
using VW.RS.IO;

namespace VW.RS.Algorithms
{
    public enum EnumResampleMethod : int
    {
        NearestNeighbour = ResampleAlg.GRA_NearestNeighbour,
        Bilinear = ResampleAlg.GRA_Bilinear,
        Cubic = ResampleAlg.GRA_Cubic,
        Average = ResampleAlg.GRA_Average,
        CubicSpline = ResampleAlg.GRA_CubicSpline,
        Lanczos = ResampleAlg.GRA_Lanczos,
        Mode = ResampleAlg.GRA_Mode
    }

    /// <summary>
    /// 重采样
    /// </summary>
    public class Resample
    {
        private double progStep = 0.1;
        private double prog = 0.1;

        public void Run(string fileIn, string fileOut, double cellSizeX, double cellSizeY, int method)
        {
            GDALRead rasterIn = new GDALRead(fileIn, Access.GA_ReadOnly);

            if (rasterIn.CellSizeX == cellSizeX && rasterIn.CellSizeY == cellSizeY)
            {
                rasterIn.Dispose();
                File.Delete(fileOut);
                File.Copy(fileIn, fileOut);
                return;
            }

            int columnOut = (int)Math.Round(Math.Abs(rasterIn.XMax - rasterIn.XMin) / cellSizeX);
            int rowOut = (int)Math.Round(Math.Abs(rasterIn.YMax - rasterIn.YMin) / cellSizeY);

            File.Delete(fileOut);
            Driver driverOut = Gdal.GetDriverByName("GTiff");
            Dataset dsOut = driverOut.Create(fileOut, columnOut, rowOut, rasterIn.BandCount, rasterIn.DataType, null);
            dsOut.SetProjection(rasterIn.Projection);

            double[] transformOut = new double[6];
            transformOut[0] = rasterIn.XMin;
            transformOut[1] = cellSizeX;
            transformOut[2] = 0;
            transformOut[3] = rasterIn.YMax;
            transformOut[4] = 0;
            transformOut[5] = -cellSizeY;
            dsOut.SetGeoTransform(transformOut);

            for (int band = 0; band < rasterIn.BandCount; band++)
            {
                dsOut.GetRasterBand(band + 1).SetNoDataValue(double.IsNaN(rasterIn.NoDataValue) ? -3000 : rasterIn.NoDataValue);
            }

            Gdal.ReprojectImage(rasterIn.DS, dsOut, rasterIn.Projection, rasterIn.Projection, (ResampleAlg)method, 0.0, 0.0, null, "", null);
            //Gdal.ReprojectImage(rasterIn.DS, dsOut, rasterIn.Projection, rasterIn.Projection, (ResampleAlg)method, 0.0, 0.0, new Gdal.GDALProgressFuncDelegate(ProgressFunc), "", null);

            rasterIn.Dispose();
            dsOut.FlushCache();
            dsOut.Dispose();
        }

        private int ProgressFunc(double complete, IntPtr message, IntPtr data)
        {
            if (complete > prog)
            {
                prog += progStep;
            }
            return 1;
        }
    }
}