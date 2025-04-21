using OSGeo.GDAL;
using System;
using System.Collections.Generic;
using System.IO;
using VW.RS;
using VW.RS.Algorithms;
using VW.RS.IO;
using VW.RS.Utility;

namespace VW.Ecology
{
	public struct NPPPara
	{
		public int ClassType;
		public double NDVIMax;
		public double NDVIMin;
		//public double SRMax;
		//public double SRMin;
		public double EMax;
		public string Remark;
	}

	/// <summary>
	/// 计算净初级生产力（CASANPP）
	/// </summary>
	public class CASANPP
	{
		private int columnOut;
		private int rowOut;

        public void Run(string fileNDVI, string fileClass, string fileSOL, string[] fileTemperature, int indexCurrentTemperature, string fileOT, string fileRain, string fileOut)
		{
            InitGDAL.Instance.Init();
            string filePara = Directory.GetCurrentDirectory() + "\\config\\NPPPara.txt";

			if (!File.Exists(filePara))
				throw new Exception("参数文件 NPPPara.txt 不存在！");

			int bandClass = 0;
			int bandNDVI = 0;
			int bandSOL = 0;
			int bandT = 0;
			int bandRain = 0;

			//处理分类数据的分辨率
			GDALRead rasterNDVI = new GDALRead(fileNDVI, Access.GA_ReadOnly);
			GDALRead rasterClass = new GDALRead(fileClass, Access.GA_ReadOnly);

			if (rasterNDVI.CellSizeX != rasterClass.CellSizeX || rasterNDVI.CellSizeY != rasterClass.CellSizeY)
			{

				rasterClass.Dispose();

				string fileClassRes = fileClass.Substring(0, fileClass.LastIndexOf('.')) + "_resample.tif";
				new Resample().Run(fileClass, fileClassRes, rasterNDVI.CellSizeX, rasterNDVI.CellSizeY, Convert.ToInt32(EnumResampleMethod.NearestNeighbour));

				rasterClass = new GDALRead(fileClassRes, Access.GA_ReadOnly);
			}

			//读取数据
			double[] dataNDVI = new double[rasterNDVI.Column * rasterNDVI.Row];
			rasterNDVI.DS.GetRasterBand(bandNDVI + 1).ReadRaster(0, 0, rasterNDVI.Column, rasterNDVI.Row, dataNDVI, rasterNDVI.Column, rasterNDVI.Row, 0, 0);

			double[] dataClass = new double[rasterClass.Column * rasterClass.Row];
			rasterClass.DS.GetRasterBand(bandClass + 1).ReadRaster(0, 0, rasterClass.Column, rasterClass.Row, dataClass, rasterClass.Column, rasterClass.Row, 0, 0);

			GDALRead rasterSOL = new GDALRead(fileSOL, Access.GA_ReadOnly);
			double[] dataSOL = new double[rasterNDVI.Column * rasterNDVI.Row];
			rasterSOL.DS.GetRasterBand(bandSOL + 1).ReadRaster(0, 0, rasterSOL.Column, rasterSOL.Row, dataSOL, rasterSOL.Column, rasterSOL.Row, 0, 0);

			GDALRead rasterT = new GDALRead(fileTemperature[indexCurrentTemperature], Access.GA_ReadOnly);
			GDALRead rasterOT = new GDALRead(fileOT, Access.GA_ReadOnly);
			double[] dataT = new double[rasterT.Column * rasterT.Row];
			double[] dataOT = new double[rasterOT.Column * rasterOT.Row];
			rasterT.DS.GetRasterBand(bandT + 1).ReadRaster(0, 0, rasterT.Column, rasterT.Row, dataT, rasterT.Column, rasterT.Row, 0, 0);
			rasterOT.DS.GetRasterBand(bandT + 1).ReadRaster(0, 0, rasterOT.Column, rasterOT.Row, dataOT, rasterOT.Column, rasterOT.Row, 0, 0);

			GDALRead rasterRain = new GDALRead(fileRain, Access.GA_ReadOnly);
			double[] dataRain = new double[rasterRain.Column * rasterRain.Row];
			rasterRain.DS.GetRasterBand(bandRain + 1).ReadRaster(0, 0, rasterRain.Column, rasterRain.Row, dataRain, rasterRain.Column, rasterRain.Row, 0, 0);

			double[] dataT_I = new double[rasterT.Column * rasterT.Row];
			for (int i = 0; i < columnOut * rowOut; i++)
			{
				dataT_I[0] = 0;
			}

			columnOut = rasterNDVI.Column;
			rowOut = rasterNDVI.Row;

			for (int f = 0; f < fileTemperature.Length; f++)
			{
				double[] buf = new double[rasterT.Column * rasterT.Row];
				GDALRead rasterT_Month = new GDALRead(fileTemperature[f], Access.GA_ReadOnly);
				rasterT_Month.DS.GetRasterBand(bandT + 1).ReadRaster(0, 0, rasterT_Month.Column, rasterT_Month.Row, buf, rasterT_Month.Column, rasterT_Month.Row, 0, 0);

				for (int i = 0; i < columnOut * rowOut; i++)
				{
					if (buf[i] == rasterT_Month.NoDataValue || double.IsNaN(buf[i]))
						continue;
					else
					{
						buf[i] = buf[i] < 0 ? 0 : buf[i];
						dataT_I[i] += System.Math.Pow(buf[i] / 5, 1.514);

					}
				}

				rasterT_Month.Dispose();
			}

			NPPPara[] paraTable = GetStaticParaTable(filePara);
            double[] dataApar;
            double[] dataFpar;
            double[] dataFparNDVI;
            double[] dataFparSR;
            APAR(dataNDVI, rasterNDVI.NoDataValue, dataClass, dataSOL, rasterSOL.NoDataValue, paraTable, out  dataApar, out  dataFpar, out dataFparNDVI, out dataFparSR);

			double[] dataTemperature = Temperature(rasterNDVI.NoDataValue, dataT, rasterT.NoDataValue, dataOT, rasterOT.NoDataValue);

			double[] dataMoisture = Moisture(dataT_I, dataT, dataRain, rasterT.NoDataValue, rasterRain.NoDataValue);

			double[] dataEMax = EMax(rasterNDVI.NoDataValue, dataClass, rasterClass.NoDataValue, paraTable);


			double[] dataOut = new double[columnOut * rowOut];
			for (int i = 0; i < columnOut * rowOut; i++)
			{
				if (dataApar[i] == rasterNDVI.NoDataValue || double.IsNaN(dataApar[i]) ||
					dataTemperature[i] == rasterNDVI.NoDataValue || double.IsNaN(dataTemperature[i]) ||
					dataMoisture[i] == rasterNDVI.NoDataValue || double.IsNaN(dataMoisture[i]) ||
					dataEMax[i] == rasterNDVI.NoDataValue || double.IsNaN(dataEMax[i])
					)
					dataOut[i] = rasterNDVI.NoDataValue;
				else
				{
					//NPP = APAR * 实际光能利用率   -----------------（1）
					//实际光能利用率 = 温度胁迫 * 水份胁迫 * 最大光能利用率   -----------------（2）
					dataOut[i] = dataApar[i] * dataTemperature[i] * dataMoisture[i] * dataEMax[i];
				}
			}

			//保存结果
			SaveFile(rasterNDVI, fileOut, dataOut, "");

			rasterClass.Dispose();
			rasterNDVI.Dispose();
			rasterSOL.Dispose();
			rasterT.Dispose();
			rasterOT.Dispose();
			rasterRain.Dispose();
		}

		private void SaveFile(GDALRead rasterRef, string fileOut, double[] dataOut, string tmp)
		{
			string fileTmp = fileOut.Substring(0, fileOut.LastIndexOf('.')) + tmp + ".tif";

			//输出图像
			File.Delete(fileTmp);
			Driver driver = Gdal.GetDriverByName("GTiff");
			Dataset dsout = driver.Create(fileTmp, columnOut, rowOut, 1, DataType.GDT_Float32, null);

			dsout.GetRasterBand(1).WriteRaster(0, 0, columnOut, rowOut, dataOut, columnOut, rowOut, 0, 0);
			dsout.GetRasterBand(1).SetNoDataValue(rasterRef.NoDataValue);
			dsout.SetProjection(rasterRef.Projection);
			dsout.SetGeoTransform(rasterRef.Transform);

			dsout.FlushCache();
			dsout.Dispose();
		}

		private void APAR(double[] dataNDVI, double nodataNDVI, double[] dataClass, double[] dataSOL, double nodataSOL, NPPPara[] paraTable, out double[] dataApar, out double[] dataFpar, out double[] dataFparNDVI, out double[] dataFparSR)
		{
			//NDVIStatistic(bandNDVI, 0.05, out double maxNdvi, out double minNdvi);

			//SRStatistic(dataNDVI, nodataNDVI, dataClass, paraTable, out double[] maxSR, out double[] minSR);

			dataFpar = new double[columnOut * rowOut];
			dataFparNDVI = new double[columnOut * rowOut];
			dataFparSR = new double[columnOut * rowOut];
			dataApar = new double[columnOut * rowOut];

			for (int i = 0; i < columnOut * rowOut; i++)
			{
				if (dataNDVI[i] == nodataNDVI || double.IsNaN(dataNDVI[i]) ||
					dataSOL[i] == nodataSOL || double.IsNaN(dataSOL[i]))
				{
					dataFpar[i] = nodataNDVI;
					dataFparNDVI[i] = nodataNDVI;
					dataFparSR[i] = nodataNDVI;
					dataApar[i] = nodataNDVI;
				}
				else
				{
					dataFpar[i] = 0;
					dataFparNDVI[i] = 0;
					dataFparSR[i] = 0;
					dataApar[i] = 0;
					for (int j = 0; j < paraTable.Length; j++)
					{
						if (dataClass[i] == paraTable[j].ClassType)
						{
							//FPAR_NDVI   -----------------（3-2）
							if (dataNDVI[i] < paraTable[j].NDVIMin)
							{
								dataNDVI[i] = paraTable[j].NDVIMin;
								dataFparNDVI[i] = 0.01;
							}
							else if (dataNDVI[i] > paraTable[j].NDVIMax)
							{
								dataNDVI[i] = paraTable[j].NDVIMax;
								dataFparNDVI[i] = 0.95;
							}
							else
							{
								dataFparNDVI[i] = (dataNDVI[i] - paraTable[j].NDVIMin) / (paraTable[j].NDVIMax - paraTable[j].NDVIMin) * 0.949 + 0.001;
							}

							//FPAR_SR   -----------------（3-3）

							double SR = (1 + dataNDVI[i]) / (1 - dataNDVI[i]);
							double SRMax = (1 + paraTable[j].NDVIMax) / (1 - paraTable[j].NDVIMax);
							double SRMin = (1 + paraTable[j].NDVIMin) / (1 - paraTable[j].NDVIMin);

							if (SR < SRMin)
								dataFparSR[i] = 0.01;
							else if (SR > SRMax)
								dataFparSR[i] = 0.95;
							else
								dataFparSR[i] = (SR - SRMin) / (SRMax - SRMin) * 0.949 + 0.001;

							//FPAR = FPAR_NDVI * 0.5 + FPAR_SR * 0.5   -----------------（3-1）
							dataFpar[i] = dataFparNDVI[i] * 0.5 + dataFparSR[i] * 0.5;

							//APAR = FPAR * 太阳总辐射 * 0.5   -----------------（3）
							dataApar[i] = dataFpar[i] * dataSOL[i] * 0.5;
							break;
						}
					}
				}
			}
		}

		private double[] Temperature(double nodata, double[] dataT, double nodataT, double[] dataOT, double nodataOT)
		{
			double[] dataOut = new double[columnOut * rowOut];
			double Te1, Te2;

			for (int i = 0; i < columnOut * rowOut; i++)
			{
				if (dataT[i] == nodataT || double.IsNaN(dataT[i]) ||
					dataOT[i] == nodataOT || double.IsNaN(dataOT[i]))
					dataOut[i] = nodata;
				else
				{
					//温度胁迫系数1   -----------------（4-1）
					Te1 = dataOT[i] > -10 ? 0.8 + 0.02 * dataOT[i] - 0.0005 * dataOT[i] * dataOT[i] : 0;

					//温度胁迫系数2   -----------------（4-2）
					if (dataT[i] - dataOT[i] >= 10 || dataOT[i] - dataT[i] >= 13)
					{
						Te2 = 1.184 / ((1.0f + System.Math.Exp(0.2 * -10)) * 1 / (1 + System.Math.Exp(0.3 * -10))) / 2;
					}
					else
					{
						Te2 = 1.184 / ((1.0f + System.Math.Exp(0.2 * (dataOT[i] - dataT[i] - 10))) * 1 / (1 + System.Math.Exp(0.3 * (dataT[i] - dataOT[i] - 10))));
					}

					//温度胁迫 = 温度胁迫系数1 * 温度胁迫系数2   -----------------（4）
					dataOut[i] = Te1 * Te2;

					if (dataOut[i] < 0)
						dataOut[i] = 0;
					else if (dataOut[i] > 1)
						dataOut[i] = 1;
				}
			}
			return dataOut;
		}

		private double[] Moisture(double[] bufT_I, double[] bufT, double[] bufP, double nodataT, double nodataP)
		{
			double[] bufOut = new double[columnOut * rowOut];

			for (int i = 0; i < columnOut * rowOut; i++)
			{
				if (bufT[i] == nodataT || double.IsNaN(bufT[i]) || 
					bufT_I[i] == nodataT || double.IsNaN(bufT_I[i]) || 
					bufP[i] == nodataP || double.IsNaN(bufP[i]))
					bufOut[i] = nodataT;
				else
				{
					bufP[i] = bufP[i] > 0 ? bufP[i] : 0;
					double Ix = bufT_I[i];
					double ax = (0.675 * Ix * Ix * Ix - 77.1 * Ix * Ix + 17920 * Ix + 492390) * 0.000001;
					double ep = Ix == 0 ? 0 : 16 * Math.Pow(10 * bufT[i] / Ix, ax);
					double rn = Math.Sqrt(ep * bufP[i]) * (0.369 + 0.589 * Math.Sqrt(ep / bufP[i]));
					double EET = (bufP[i] * rn * (rn * rn + bufP[i] * bufP[i] + rn * bufP[i])) / ((rn + bufP[i]) * (rn * rn + bufP[i] * bufP[i]));
					double EPT = (ep + EET) / 2;
					bufOut[i] = EPT == 0 ? 0 : 0.5 + 0.5 * EET / EPT;

					if (bufOut[i] < 0)
						bufOut[i] = 0;
					else if (bufOut[i] > 1)
						bufOut[i] = 1;
				}

		
			}
			return bufOut;
		}

		private double[] EMax(double nodata, double[] dataClass, double nodataClass, NPPPara[] paraTable)
		{
			double[] dataOut = new double[columnOut * rowOut];

			for (int i = 0; i < columnOut * rowOut; i++)
			{
				if (dataClass[i] == nodataClass || double.IsNaN(dataClass[i]))
					dataOut[i] = nodata;
				else
				{
					dataOut[i] = 0;
					for (int j = 0; j < paraTable.Length; j++)
					{
						if (dataClass[i] == paraTable[j].ClassType)
						{
							dataOut[i] = paraTable[j].EMax;
							break;
						}
					}
				}
			}
			return dataOut;
		}

		private NPPPara[] GetStaticParaTable(string file)
		{
			List<NPPPara> paras = new List<NPPPara>();

			StreamReader sr = new StreamReader(file);
			string line;
			string[] arrStr;
			NPPPara para;

			while ((line = sr.ReadLine()) != null)
			{
				arrStr = line.Split(',');
				para = new NPPPara();
				para.ClassType = Convert.ToInt32(arrStr[0]);
				para.Remark = arrStr[1];
				para.NDVIMax = Convert.ToDouble(arrStr[2]);
				para.NDVIMin = Convert.ToDouble(arrStr[3]);
				//para.SRMax = Convert.ToDouble(arrStr[4]);
				//para.SRMin = Convert.ToDouble(arrStr[5]);
				para.EMax = Convert.ToDouble(arrStr[4]);
				paras.Add(para);
			}
			sr.Close();
			sr.Dispose();

			return paras.ToArray();
		}

		private void SRStatistic(double[] dataNDVI, double nodata, double[] dataClass, NPPPara[] paraTable, out double[] maxVal, out double[] minVal)
		{
			maxVal = new double[paraTable.Length];
			minVal = new double[paraTable.Length];

			for (int j = 0; j < paraTable.Length; j++)
			{
				maxVal[j] = double.MinValue;
				minVal[j] = double.MaxValue;

				for (int i = 0; i < dataNDVI.Length; i++)
				{
					if (dataClass[i] == paraTable[j].ClassType)
					{
						if (dataNDVI[i] == nodata || double.IsNaN(dataNDVI[i]))
							continue;

						if (dataNDVI[i] < paraTable[j].NDVIMin)
							dataNDVI[i] = paraTable[j].NDVIMin;
						else if (dataNDVI[i] >= paraTable[j].NDVIMax)
							dataNDVI[i] = paraTable[j].NDVIMax;

						double SR = (1 + dataNDVI[i]) / (1 - dataNDVI[i]);
						maxVal[j] = maxVal[j] > SR ? maxVal[j] : SR;
						minVal[j] = minVal[j] < SR ? minVal[j] : SR;
					}
				}
			}
		}

		private void NDVIStatistic(Band band, double confidence, out double maxVal, out double minVal)
		{
			//获取波段最大最小值
			double[] maxmin = new double[2];
			band.ComputeRasterMinMax(maxmin, 0);

			//获取波段直方图
			int buckets = 256;
			int[] panHistogram = new int[buckets];
			band.GetHistogram(maxmin[0], maxmin[1], buckets, panHistogram, 0, 0, null, null);

			//计算波段累积直方图
			int[] cumulativeHistogram = new int[buckets];
			cumulativeHistogram[0] = panHistogram[0];
			for (int i = 1; i < buckets; i++)
			{
				cumulativeHistogram[i] = cumulativeHistogram[i - 1] + panHistogram[i];
			}

			//计算给定置信区间NDVI最值
			maxVal = maxmin[1];
			minVal = maxmin[0];
			double interval = (maxVal - minVal) / buckets;
			int histogramTotal = cumulativeHistogram[buckets - 1];

			for (int i = buckets - 1; i >= 0; i--)
			{
				if (cumulativeHistogram[i] <= histogramTotal * (1 - confidence))
				{
					maxVal = minVal + interval * i;
					break;
				}
			}

			for (int i = 0; i <= buckets - 1; i++)
			{
				if (cumulativeHistogram[i] >= histogramTotal * confidence)
				{
					minVal = minVal + interval * i;
					break;
				}
			}
		}

		public static void CalOptimalTemperature(string[] fileNDVI, string[] fileT, string fileOut)
		{
			if (fileNDVI.Length != fileT.Length)
				throw new Exception("输入数据数量不正确");

			InitGDAL.Instance.Init();
			GDALRead raster0 = new GDALRead(fileT[0], Access.GA_ReadOnly);

			double noData = raster0.NoDataValue;
			int columnOut = raster0.Column;
			int rowOut = raster0.Row;
			string projection = raster0.Projection;
			double[] transform = raster0.Transform;

			raster0.Dispose();

			double[] dataNDVI = new double[columnOut * rowOut];
			double[] dataT = new double[columnOut * rowOut];
			double[] dataMaxNDVI = new double[columnOut * rowOut];
			double[] dataOutT = new double[columnOut * rowOut];

			for (int i = 0; i < columnOut * rowOut; i++)
			{
				dataMaxNDVI[i] = -1;
				dataOutT[i] = noData;
			}

			for (int f = 0; f < fileNDVI.Length; f++)
			{
				GDALRead rasterNDVI = new GDALRead(fileNDVI[f], Access.GA_ReadOnly);
				GDALRead rasterT = new GDALRead(fileT[f], Access.GA_ReadOnly);

				rasterNDVI.DS.GetRasterBand(1).ReadRaster(0, 0, columnOut, rowOut, dataNDVI, columnOut, rowOut, 0, 0);
				rasterT.DS.GetRasterBand(1).ReadRaster(0, 0, columnOut, rowOut, dataT, columnOut, rowOut, 0, 0);

				for (int i = 0; i < columnOut * rowOut; i++)
				{
					if (dataNDVI[i] == noData)
						continue;
					else
					{
						if (dataNDVI[i] > dataMaxNDVI[i])
						{
							dataMaxNDVI[i] = dataNDVI[i];
							dataOutT[i] = dataT[i];
						}
					}
				}

				rasterNDVI.Dispose();
				rasterT.Dispose();
			}

			File.Delete(fileOut);
			Driver driver = Gdal.GetDriverByName("GTiff");
			Dataset dsout = driver.Create(fileOut, columnOut, rowOut, 1, DataType.GDT_Float32, null);

			dsout.GetRasterBand(1).WriteRaster(0, 0, columnOut, rowOut, dataOutT, columnOut, rowOut, 0, 0);
			dsout.GetRasterBand(1).SetNoDataValue(noData);
			dsout.SetProjection(projection);
			dsout.SetGeoTransform(transform);

			dsout.FlushCache();
			dsout.Dispose();
		}

		public static void TransCoord(string dirIn, string dirOut)
		{
			InitGDAL.Instance.Init();

			Directory.Delete(dirOut, true);

			foreach (string fileIn in Directory.GetFiles(dirIn))
			{
				string fileOut = dirOut + "\\" + Path.GetFileName(fileIn);

				GDALRead rasterIn = new GDALRead(fileIn, Access.GA_ReadOnly);

				double[] transform = rasterIn.Transform;
				if (transform[0] == -180)
				{
					transform[0] = 0;
                }
                else
                {
					rasterIn.Dispose();
					continue;
                }

				File.Delete(fileOut);
				Driver driver = Gdal.GetDriverByName("GTiff");
				Dataset dsout = driver.Create(fileOut, rasterIn.Column, rasterIn.Row, 1, DataType.GDT_Float32, null);

				dsout.GetRasterBand(1).SetNoDataValue(rasterIn.NoDataValue);
				dsout.SetProjection(rasterIn.Projection);
				dsout.SetGeoTransform(transform);

				double[] dataIn = new double[rasterIn.Column];
				double[] dataOut = new double[rasterIn.Column];

				for (int r = 0; r < rasterIn.Row; r++)
				{
					rasterIn.DS.GetRasterBand(1).ReadRaster(0, r, rasterIn.Column, 1, dataIn, rasterIn.Column, 1, 0, 0);

					Array.Copy(dataIn, dataOut.Length / 2, dataOut, 0, dataOut.Length / 2);
					Array.Copy(dataIn, 0, dataOut, dataOut.Length / 2, dataOut.Length / 2);

					dsout.GetRasterBand(1).WriteRaster(0, r, rasterIn.Column, 1, dataOut, rasterIn.Column, 1, 0, 0);
				}

				rasterIn.Dispose();
				dsout.FlushCache();
				dsout.Dispose();
			}
		}
	}
}