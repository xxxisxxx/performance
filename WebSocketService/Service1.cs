using System;
using System.IO.Ports;
using System.Net;
using System.ServiceProcess;
using System.Text;
using System.Threading.Tasks;
using System.Diagnostics;
using System.IO;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace WebSocketService
{
    public partial class Service1 : ServiceBase
    {
        private EventLog eventLog;
        private SerialPort serialPort;
        private static string url;
        private static string USB_COM;
        private static int USB_BR;
        private static int MAX_TEMPERATURE;
        private static int MAX_SPEED;
        private System.Timers.Timer reconnectTimer;
        private string statusTxt;

        public Service1()
        {
            InitializeComponent();
            eventLog = new EventLog();
            if (!EventLog.SourceExists("DengqianServiceSource"))
            {
                EventLog.CreateEventSource("DengqianServiceSource", "DengqianServiceLog");
            }
            eventLog.Source = "DengqianServiceSource";
            eventLog.Log = "DengqianServiceLog";

            reconnectTimer = new System.Timers.Timer(2000);
            reconnectTimer.Elapsed += (sender, e) => InitSerialPort();

            LoadConfig();
        }

        protected override void OnStart(string[] args)
        {
            WriteLogEntry("服务启动中...", EventLogEntryType.Information);
            InitSerialPort();
            GetData();
        }

        protected override void OnStop()
        {
            WriteLogEntry("服务停止中...", EventLogEntryType.Information);
            serialPort?.Close();
            reconnectTimer?.Stop();
        }

        private void LoadConfig()
        {
            try
            {
                string configFilePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "config.ini");
                var config = new Dictionary<string, string>();

                foreach (var line in File.ReadAllLines(configFilePath))
                {
                    if (line.Contains("="))
                    {
                        var keyValue = line.Split(new[] { '=' }, 2);
                        if (keyValue.Length == 2)
                        {
                            config[keyValue[0].Trim()] = keyValue[1].Trim();
                        }
                    }
                }

                url = config["url"];
                USB_COM = config["USB_COM"];
                USB_BR = int.Parse(config["USB_BR"]);
                MAX_TEMPERATURE = int.Parse(config["MAX_TEMPERATURE"]);
                MAX_SPEED = int.Parse(config["MAX_SPEED"]);

                WriteLogEntry("配置文件加载成功。", EventLogEntryType.Information);
            }
            catch (Exception ex)
            {
                WriteLogEntry($"加载配置文件时出错: {ex.Message}", EventLogEntryType.Error);
                throw;
            }
        }

        private void InitSerialPort()
        {
            try
            {
                serialPort = new SerialPort(USB_COM, USB_BR);
                serialPort.Open();
                serialPort.DataReceived += SerialPort_DataReceived;
                serialPort.ErrorReceived += SerialPort_ErrorReceived;
                statusTxt = $"串口 {USB_COM} 已连接.";
                WriteLogEntry(statusTxt, EventLogEntryType.Information);
            }
            catch (Exception ex)
            {
                statusTxt = $"串口 {USB_COM} 不存在或未连接: {ex.Message}";
                WriteLogEntry(statusTxt, EventLogEntryType.Error);
                reconnectTimer.Start();
            }
        }

        private void SerialPort_DataReceived(object sender, SerialDataReceivedEventArgs e)
        {
            var data = serialPort.ReadExisting();
            //WriteLogEntry($"串口数据: {data}", EventLogEntryType.Information);
        }

        private void SerialPort_ErrorReceived(object sender, SerialErrorReceivedEventArgs e)
        {
            statusTxt = "串口错误";
            WriteLogEntry(statusTxt, EventLogEntryType.Error);
            reconnectTimer.Start();
        }

        private async void GetData()
        {
            while (true)
            {
                try
                {
                    WriteLogEntry($"发送HTTP请求到 {url}", EventLogEntryType.Information);
                    var request = WebRequest.Create(url);
                    request.Method = "GET";
                    request.ContentType = "text/event-stream";

                    using (var response = await request.GetResponseAsync())
                    using (var stream = response.GetResponseStream())
                    using (var reader = new StreamReader(stream, Encoding.UTF8))
                    {
                        var buffer = new StringBuilder();
                        string line;
                        while ((line = await reader.ReadLineAsync()) != null)
                        {
                            if (line.StartsWith("data: "))
                            {
                                buffer.AppendLine(line.Substring(5).Trim()); // 去掉 "data: " 前缀
                            }
                            else if (string.IsNullOrWhiteSpace(line) && buffer.Length > 0)
                            {
                                // 分隔符，处理完整的消息
                                var message = buffer.ToString();
                                buffer.Clear();

                              //  WriteLogEntry($"收到数据: {message}", EventLogEntryType.Information);

                                // 处理消息
                                var dataEntries = message.Split(new[] { "{|}" }, StringSplitOptions.None);
                                var txtlist = new StringBuilder();

                                foreach (var entry in dataEntries)
                                {
                                    var kvp = entry.Split('|');
                                    if (kvp.Length == 2)
                                    {
                                        var key = kvp[0];
                                        var value = kvp[1];
                                        if (double.TryParse(Regex.Match(value, @"[\d.]+").Value, out var numericValue))
                                        {
                                            var percentageValue = ConvertToPercentage(key, numericValue);
                                            txtlist.Append($"{percentageValue}|");
                                        }
                                    }
                                }

                                if (txtlist.Length > 0 && txtlist[txtlist.Length - 1] == '|')
                                {
                                    txtlist.Length--;
                                }

                                if (serialPort.IsOpen)
                                {
                                    serialPort.Write(txtlist.ToString());
                                  //  WriteLogEntry($"发送数据: {txtlist}", EventLogEntryType.Information);
                                }
                                else
                                {
                                    WriteLogEntry("串口未打开，无法发送数据", EventLogEntryType.Warning);
                                }
                            }
                        }
                    }
                }
                catch (WebException webEx)
                {
                    WriteLogEntry($"HTTP请求错误: {webEx.Message}", EventLogEntryType.Error);

                    if (webEx.Response != null)
                    {
                        using (var errorResponse = (HttpWebResponse)webEx.Response)
                        using (var reader = new StreamReader(errorResponse.GetResponseStream()))
                        {
                            string errorText = reader.ReadToEnd();
                            WriteLogEntry($"服务器响应: {errorText}", EventLogEntryType.Error);
                        }
                    }

                    await Task.Delay(2000);
                }
                catch (Exception ex)
                {
                    WriteLogEntry($"请求错误: {ex.Message}", EventLogEntryType.Error);

                    if (ex.InnerException != null)
                    {
                        WriteLogEntry($"请求内部错误: {ex.InnerException.Message}", EventLogEntryType.Error);
                    }

                    await Task.Delay(2000);
                }
            }
        }


        private double ConvertToPercentage(string key, double value)
        {
            double percentage = 0;

            if (key.Contains("Simple1") || key.Contains("Simple3") || key.Contains("Simple5"))
            {
                percentage = value / 100;
            }
            else if (key.Contains("Simple2") || key.Contains("Simple4") || key.Contains("Simple6"))
            {
                percentage = value / MAX_TEMPERATURE;
            }
            else if (key.Contains("Simple7") || key.Contains("Simple8"))
            {
                percentage = Math.Min(value / MAX_SPEED, 1);
            }
            else if (key.Contains("Simple9") || key.Contains("Simple10"))
            {
                percentage = Math.Min(value / (MAX_SPEED * 0.1), 1);
            }

            return Math.Round(percentage * 100) / 100;
        }

        private void WriteLogEntry(string message, EventLogEntryType entryType)
        {
            eventLog.WriteEntry(message, entryType);
        }
    }
}
