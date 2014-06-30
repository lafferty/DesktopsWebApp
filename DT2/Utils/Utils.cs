using DotNet.Highcharts;
using DotNet.Highcharts.Enums;
using DotNet.Highcharts.Helpers;
using DotNet.Highcharts.Options;
using DT2.Models;
using log4net;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Mail;
using System.Web;

namespace DT2.Utils
{
    /// <summary>
    /// Groups useful functionality that is distinct from the information model.
    /// </summary>
    public class Utils
    {
        static public ILog logger = LogManager.GetLogger(typeof(Utils));

        /// <summary>
        /// Mail settings are obtained from root web.config
        /// </summary>
        /// <param name="mailMessage"></param>
        public static void SendEmail(MailMessage mailMessage)
        {
            var client = new SmtpClient();
            try
            {
                logger.Debug("Sending email: " + mailMessage.ToString());
                client.Send(mailMessage);
            }
            catch (Exception ex)
            {
                logger.Error("Error sending email " + ex.Message);
            }
        }

        #region charts

        public static Highcharts StackedBar()
        {
            //List<String>  catalogNames = new List<string>();
            //catalogNames.Add("Connect 2014");
            //catalogNames.Add("Connect UK");
            //catalogNames.Add("IGS Sample");

            List<String> catalogNames = new List<string>();
            var catalogList = Catalog.GetCatalogs();
            List<object> stoppedMachines = new List<object>();
            List<object> runningOnlyMachines = new List<object>();
            List<object> usedMachines = new List<object>();

            foreach (var cat in catalogList)
            {
                catalogNames.Add(cat.Name);

                var machines = Machine.GetMachines(cat.Name);
                int stopped = 0;
                int runningOnly = 0;
                int used = 0;
                foreach (var machine in machines)
                {
                    if (machine.SessionCount > 0)
                    {
                        used++;
                    }
                    else if (machine.PowerState.ToLowerInvariant().Equals("off"))
                    {
                        stopped++;
                    }
                    else
                    {
                        runningOnly++;
                    }
                }

                stoppedMachines.Add(stopped);
                runningOnlyMachines.Add(runningOnly);
                usedMachines.Add(used);
            }

            Highcharts chart = new Highcharts("barchart")
                .InitChart(new Chart { DefaultSeriesType = ChartTypes.Bar })
                .SetTitle(new Title { Text = "Desktop Group Usage" })
                .SetXAxis(new XAxis { Categories = catalogNames.ToArray() })
                .SetYAxis(new YAxis
                {
                    Min = 0,
                    Title = new YAxisTitle { Text = "Desktop Count" },
                    TickInterval = 1
                })
                .SetTooltip(new Tooltip { Formatter = "function() { return ''+ this.series.name +': '+ this.y +''; }" })
                .SetPlotOptions(new PlotOptions { Bar = new PlotOptionsBar { Stacking = Stackings.Normal } })
                .SetSeries(new[]
                    {
                        //new Series { Name = "Stopped", Data = new Data(new object[] { 0, 0, 3 }), Color = System.Drawing.Color.LightBlue},
                        //new Series { Name = "Running, No Session ", Data = new Data(new object[] { 0, 1, 0 }), Color = System.Drawing.Color.OrangeRed },
                        //new Series { Name = "With Session", Data = new Data(new object[] { 1, 0, 1 }), Color = System.Drawing.Color.Green }
                        new Series { Name = "Off", Data = new Data(stoppedMachines.ToArray()), Color = System.Drawing.Color.LightBlue},
                        new Series { Name = "On, but unused", Data = new Data(runningOnlyMachines.ToArray()), Color = System.Drawing.Color.OrangeRed },
                        new Series { Name = "In Use", Data = new Data(usedMachines.ToArray()), Color = System.Drawing.Color.Green }
                    });

            return chart;
        }

        //public static Highcharts DonutChart()
        //{
        //    List<String> categoriesList = new List<string>();
        //    List<Data> dataList = new List<Data>();

        //    string[] categories = new[] { "MSIE", "Firefox", "Chrome", "Safari", "Opera" };
        //    Data data = new Data(new[]
        //        {
        //            new Point
        //                {
        //                    Y = 55.11,
        //                    Color = System.Drawing.Color.FromName("colors[0]"),
        //                    Drilldown = new Drilldown
        //                        {
        //                            Name = "MSIE versions",
        //                            Categories = new[] { "MSIE 6.0", "MSIE 7.0", "MSIE 8.0", "MSIE 9.0" },
        //                            Data = new Data(new object[] { 10.85, 7.35, 33.06, 2.81 }),
        //                            Color = System.Drawing.Color.FromName("colors[0]")
        //                        }
        //                },
        //            new DotNet.Highcharts.Options.Point
        //                {
        //                    Y = 21.63,
        //                    Color = System.Drawing.Color.FromName("colors[1]"),
        //                    Drilldown = new Drilldown
        //                        {
        //                            Name = "Firefox versions",
        //                            Categories = new[] { "Firefox 2.0", "Firefox 3.0", "Firefox 3.5", "Firefox 3.6", "Firefox 4.0" },
        //                            Data = new Data(new object[] { 0.20, 0.83, 1.58, 13.12, 5.43 }),
        //                            Color = System.Drawing.Color.FromName("colors[1]")
        //                        }
        //                },
        //            new Point
        //                {
        //                    Y = 11.94,
        //                    Color = System.Drawing.Color.FromName("colors[2]"),
        //                    Drilldown = new Drilldown
        //                        {
        //                            Name = "Chrome versions",
        //                            Categories = new[] { "Chrome 5.0", "Chrome 6.0", "Chrome 7.0", "Chrome 8.0", "Chrome 9.0", "Chrome 10.0", "Chrome 11.0", "Chrome 12.0" },
        //                            Data = new Data(new object[] { 0.12, 0.19, 0.12, 0.36, 0.32, 9.91, 0.50, 0.22 }),
        //                            Color = System.Drawing.Color.FromName("colors[2]")
        //                        }
        //                },
        //            new Point
        //                {
        //                    Y = 7.15,
        //                    Color = System.Drawing.Color.FromName("colors[3]"),
        //                    Drilldown = new Drilldown
        //                        {
        //                            Name = "Safari versions",
        //                            Categories = new[] { "Safari 5.0", "Safari 4.0", "Safari Win 5.0", "Safari 4.1", "Safari/Maxthon", "Safari 3.1", "Safari 4.1" },
        //                            Data = new Data(new object[] { 4.55, 1.42, 0.23, 0.21, 0.20, 0.19, 0.14 }),
        //                            Color = System.Drawing.Color.FromName("colors[3]")
        //                        }
        //                },
        //            new Point
        //                {
        //                    Y = 2.14,
        //                    Color = System.Drawing.Color.FromName("colors[4]"),
        //                    Drilldown = new Drilldown
        //                        {
        //                            Name = "Opera versions",
        //                            Categories = new[] { "Opera 9.x", "Opera 10.x", "Opera 11.x" },
        //                            Data = new Data(new object[] { 0.12, 0.37, 1.65 }),
        //                            Color = System.Drawing.Color.FromName("colors[4]")
        //                        }
        //                }
        //        });

        //    List<Point> browserDataList = new List<Point>(categoriesList.Count);
        //    List<Point> versionsDataList = new List<Point>();

        //    List<Point> browserData = new List<Point>(categories.Length);
        //    List<Point> versionsData = new List<Point>();
        //    for (int i = 0; i < categories.Length; i++)
        //    {
        //        browserData.Add(new Point { Name = categories[i], Y = data.SeriesData[i].Y, Color = data.SeriesData[i].Color });
        //        for (int j = 0; j < data.SeriesData[i].Drilldown.Categories.Length; j++)
        //        {
        //            Drilldown drilldown = data.SeriesData[i].Drilldown;
        //            versionsData.Add(new Point { Name = drilldown.Categories[j], Y = Number.GetNumber(drilldown.Data.ArrayData[j]), Color = drilldown.Color });
        //        }
        //    }

        //    Highcharts chart = new Highcharts("chart")
        //        .InitChart(new Chart { DefaultSeriesType = ChartTypes.Pie })
        //        .SetTitle(new Title { Text = "Browser market share, April, 2011" })
        //        .SetSubtitle(new Subtitle { Text = "Total percent market share" })
        //        .SetPlotOptions(new PlotOptions { Pie = new PlotOptionsPie { Shadow = false } })
        //        .SetTooltip(new Tooltip { Formatter = @"function() { return '<b>'+ this.point.name +'</b>: '+ this.y +' %';}" })
        //        .AddJavascripVariable("colors", "Highcharts.getOptions().colors")
        //        .SetSeries(new[]
        //            {
        //                new Series
        //                    {
        //                        Name = "Browsers",
        //                        Data = new Data(browserData.ToArray()),
        //                        PlotOptionsPie = new PlotOptionsPie
        //                            {
        //                                Size = new PercentageOrPixel(60, true),
        //                                DataLabels = new PlotOptionsPieDataLabels
        //                                    {
        //                                        Formatter = "function() { return this.y > 5 ? this.point.name : null; }",
        //                                        Color = System.Drawing.Color.White,
        //                                        Distance = -30
        //                                    }
        //                            }
        //                    },
        //                new Series
        //                    {
        //                        Name = "Versions",
        //                        Data = new Data(versionsData.ToArray()),
        //                        PlotOptionsPie = new PlotOptionsPie
        //                            {
        //                                InnerSize = new PercentageOrPixel(60, true),
        //                                DataLabels = new PlotOptionsPieDataLabels
        //                                    {
        //                                        Formatter = "function() { return this.y > 1 ? '<b>'+ this.point.name +':</b> '+ this.y +'%'  : null; }"
        //                                    }
        //                            }
        //                    }
        //            });

        //    return chart;
        //}

        public static Highcharts PieChart()
        {
            Highcharts chart = new Highcharts("chart")
                .InitChart(new Chart { PlotShadow = false })
                .SetTitle(new Title { Text = "Desktop Groups" })
                .SetTooltip(new Tooltip { Formatter = "function() { return '<b>'+ this.point.name +'</b>: '+ this.percentage.toFixed(2) +' %'; }" })
                .SetPlotOptions(new PlotOptions
                {
                    Pie = new PlotOptionsPie
                    {
                        AllowPointSelect = true,
                        Cursor = Cursors.Pointer,
                        DataLabels = new PlotOptionsPieDataLabels
                        {
                            Color = System.Drawing.ColorTranslator.FromHtml("#000000"),
                            ConnectorColor = System.Drawing.ColorTranslator.FromHtml("#000000"),
                            Formatter = "function() { return '<b>'+ this.point.name +'</b>: '+ this.y +' desktops'; }"
                        }
                    }
                });
            var catalogList = Catalog.GetCatalogs();
            List<object> objList = new List<object>();
            foreach (var cat in catalogList)
            {
                var tmp = new object[]
                {
                    cat.Name, cat.Count
                };
                objList.Add(tmp);
            }
            object[] dataArray = objList.ToArray();
            chart.SetSeries(new Series
            {
                Type = ChartTypes.Pie,
                Name = "Browser share",
                Data = new Data(dataArray)
                //Data = new Data(new object[]
                //        {
                //            new object[] { "Office", 8 },
                //            new object[] { "Interns", 2},
                //            new object[] { "Developers", 2},
                //        })
            });

            return chart;
        }

        public static Highcharts PieWithGradientFill(List<Catalog> catalogList)
        {
            Highcharts chart = new Highcharts("piechart")
                .InitChart(new Chart { PlotBackgroundColor = null, PlotBorderWidth = null, PlotShadow = false })
                .SetTitle(new Title { Text = catalogList.Count>0 ? "Overview of Desktop Groups": string.Empty })
                .SetTooltip(new Tooltip { Formatter = "function() { return '<b>'+ this.point.name +'</b>: '+ this.percentage.toFixed(2) +' %'; }" })
                .SetPlotOptions(new PlotOptions
                {
                    Pie = new PlotOptionsPie
                    {
                        AllowPointSelect = true,
                        Cursor = Cursors.Pointer,
                        DataLabels = new PlotOptionsPieDataLabels
                        {
                            Enabled = true,
                            Color = System.Drawing.ColorTranslator.FromHtml("#000000"),
                            ConnectorColor = System.Drawing.ColorTranslator.FromHtml("#000000"),
                            Formatter = "function() { return '<b>'+ this.point.name +'</b>: '+ this.y +' desktops'; }"
                        }
                    }
                });
            List<object> objList = new List<object>();
            foreach (var cat in catalogList)
            {
                var tmp = new object[]
                {
                    cat.Name, cat.Count
                };
                objList.Add(tmp);
            }
            object[] dataArray = objList.ToArray();
            chart.SetSeries(new Series
            {
                Type = ChartTypes.Pie,
                Name = "Browser share",
                Data = new Data(dataArray)
                //Data = new Data(new object[]
                //        {
                //            new object[] { "Office", 8 },
                //            new object[] { "Interns", 2},
                //            new object[] { "Developers", 2},
                //        })
            });
            return chart;
        }

        #endregion

    }
}
