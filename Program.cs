using VW.Ecology;
using System;
using System.Xml;
using System.IO;

namespace casanpp
{
    internal class Program
    {
        static void Main(string[] args)
        {
            /// <param name="fileNDVI">输入NDVI数据</param>
            /// <param name="fileClass">输入土地覆盖分类数据</param>
            /// <param name="fileSOL">输入月太阳辐射量数据</param>
            /// <param name="fileTemperature">输入月平均温度序列数据</param>
            /// <param name="indexCurrentTemperature">本月平均温度数据序号</param>
            /// <param name="fileOT">输入最适温度数据</param>
            /// <param name="fileRain">输入月累计降水量数据</param>
            /// <param name="fileOut">输出数据</param>

            string fileNDVI="";
            string fileClass = ""; 
            string fileSOL = ""; ;
            string fileTemperature = "";
            string fileOT = "";
            string fileRain = "";
            string fileOut = "";

            string sXmlPath = Directory.GetCurrentDirectory() + "\\config\\Config.xml";
            if (File.Exists(sXmlPath))
            {
                XmlDocument xmlConfig = new XmlDocument();
                xmlConfig.Load(sXmlPath);
                XmlNode nodeTime = xmlConfig.SelectSingleNode("//configuration//TimeConfig");
                XmlNodeList NodeListTime = nodeTime.ChildNodes;
                int startYear;
                int endYear;
                int startMonth;
                int endMonth;
                startYear = Convert.ToInt16(NodeListTime.Item(0).Attributes["StartYear"].Value);
                endYear = Convert.ToInt16(NodeListTime.Item(0).Attributes["EndYear"].Value);
                startMonth = Convert.ToInt16(NodeListTime.Item(0).Attributes["StartMonth"].Value);
                endMonth = Convert.ToInt16(NodeListTime.Item(0).Attributes["EndMonth"].Value);

                XmlNode nodePath = xmlConfig.SelectSingleNode("//configuration//PathConfig");
                XmlNodeList NodeListPath = nodePath.ChildNodes;
                foreach (XmlNode xmlNode in NodeListPath)
                {
                    if (xmlNode.Attributes["DataName"].Value == "NDVI")
                    {
                        fileNDVI = xmlNode.Attributes["DataPath"].Value + xmlNode.Attributes["DataPrefix"].Value;
                    }
                    if (xmlNode.Attributes["DataName"].Value == "PRCP")
                    {
                        fileRain = xmlNode.Attributes["DataPath"].Value + xmlNode.Attributes["DataPrefix"].Value;
                    }
                    if (xmlNode.Attributes["DataName"].Value == "LUCC")
                    {
                        fileClass = xmlNode.Attributes["DataPath"].Value + xmlNode.Attributes["DataPrefix"].Value;
                    }
                    if (xmlNode.Attributes["DataName"].Value == "Solar Radiation")
                    {
                        fileSOL = xmlNode.Attributes["DataPath"].Value + xmlNode.Attributes["DataPrefix"].Value ;
                    }
                    if (xmlNode.Attributes["DataName"].Value == "Optimal Air Temperature")
                    {
                        fileOT = xmlNode.Attributes["DataPath"].Value + xmlNode.Attributes["DataPrefix"].Value ;
                    }
                    if (xmlNode.Attributes["DataName"].Value == "Results")
                    {
                        fileOut = xmlNode.Attributes["DataPath"].Value + xmlNode.Attributes["DataPrefix"].Value;
                    }
                    if (xmlNode.Attributes["DataName"].Value == "Air Temperature")
                    {
                        fileTemperature = xmlNode.Attributes["DataPath"].Value + xmlNode.Attributes["DataPrefix"].Value;
                    }
                }
                for (int year = startYear; year <= endYear; year++)
                {
                    for (int month = startMonth; month <= endMonth; month++)
                    {                        
                        string pathNDVI= fileNDVI + year.ToString().PadLeft(4, '0') + month.ToString().PadLeft(2, '0') + ".tif";
                        string pathClass = fileClass + year.ToString().PadLeft(4, '0') + ".tif";
                        string pathSOL = fileSOL + year.ToString().PadLeft(4, '0') + month.ToString().PadLeft(2, '0') + ".tif";
                        string[] pathTemperature = new string[12];
                        for (int i = 0; i < 12; i++)
                        {
                            pathTemperature[i] = fileTemperature + year.ToString().PadLeft(4, '0') + month.ToString().PadLeft(2, '0') + ".tif";
                        }
                        string pathOT = fileOT + year.ToString().PadLeft(4, '0') + ".tif";
                        string pathRain = fileRain + year.ToString().PadLeft(4, '0') + month.ToString().PadLeft(2, '0') + ".tif";
                        string pathOut = fileOut + year.ToString().PadLeft(4, '0') + month.ToString().PadLeft(2, '0') + ".tif";                        
                        new CASANPP().Run(pathNDVI, pathClass, pathSOL, pathTemperature, month - 1, pathOT, pathRain, pathOut);
                    }
                }
                

                
            }

            

        }
    }
}
