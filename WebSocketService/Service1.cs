using System;
using System.Diagnostics;
using System.ServiceProcess;
using System.Threading;
using Newtonsoft.Json;

namespace WebSocketService
{
    public partial class Service1 : ServiceBase
    {
        private readonly string serverAddress = "wss://eye.xxisxx.net:1030/?user=dq&pc=1"; // 修改为实际的 WebSocket 服务器地址
        private WebSocket4Net.WebSocket websocket;
        private bool isWebSocketConnected = false;

        // 事件日志对象
        private EventLog eventLog;

        public Service1()
        {
            InitializeComponent();
            // 设置事件日志
            eventLog = new EventLog();
            if (!EventLog.SourceExists("WebSocketServiceSource"))
            {
                EventLog.CreateEventSource("WebSocketServiceSource", "WebSocketServiceLog");
            }
            eventLog.Source = "WebSocketServiceSource";
            eventLog.Log = "WebSocketServiceLog";
        }

        protected override void OnStart(string[] args)
        {
            StartWebSocket();
            StartPerformanceMonitoring();
        }

        protected override void OnStop()
        {
            StopPerformanceMonitoring();
            StopWebSocket();
        }

        private void StartWebSocket()
        {
            websocket = new WebSocket4Net.WebSocket(serverAddress,SslConfiguration.Default);

            websocket.Opened += (sender, e) =>
            {
                isWebSocketConnected = true;
                // 写入连接状态到事件日志
                WriteLogEntry("WebSocket connected", EventLogEntryType.Information);
            };

            websocket.Closed += (sender, e) =>
            {
                isWebSocketConnected = false;
                // 写入连接状态到事件日志
                WriteLogEntry("WebSocket disconnected 断开连接 也许是无法连接到服务器", EventLogEntryType.Warning);
                // 断开后尝试重新连接
                Thread.Sleep(5000); // 等待5秒后重新连接
                StartWebSocket();
            };

            websocket.Open();
        }

        private void StopWebSocket()
        {
            if (websocket != null && websocket.State == WebSocket4Net.WebSocketState.Open)
            {
                websocket.Close();
            }
        }

        private void StartPerformanceMonitoring()
        {
            Timer timer = new Timer(UpdatePerformanceData, null, TimeSpan.Zero, TimeSpan.FromSeconds(5)); // 每5秒更新一次性能数据
        }

        private void StopPerformanceMonitoring()
        {
            // 在此停止性能监控的逻辑，如果有的话
        }

        private void UpdatePerformanceData(object state)
        {
            PerformanceCounter cpuCounter = new PerformanceCounter("Processor", "% Processor Time", "_Total");
            PerformanceCounter ramCounter = new PerformanceCounter("Memory", "Available MBytes");

            float cpuUsage = cpuCounter.NextValue();
            float ramUsage = ramCounter.NextValue();

            // 获取机器码、处理器数量和操作系统版本
            string machineName = Environment.MachineName;
            int processorCount = Environment.ProcessorCount;
            Version osVersion = Environment.OSVersion.Version;

            // 构造要发送的 JSON 数据
            var data = new
            {
                main = 201,
                type = 0,
                body = JsonConvert.SerializeObject(new
                {
                    machineName,
                    processorCount,
                    osVersion,
                    cpu = cpuUsage,
                    ram = ramUsage
                    // 如果还需要其他性能数据，可以继续添加在这里
                })
            };

            string jsonData = JsonConvert.SerializeObject(data);

            // 发送数据到 WebSocket 服务器
            if (isWebSocketConnected)
            {
                websocket.Send(jsonData);
            }
            else
            {
                // 写入错误信息到事件日志
                WriteLogEntry("WebSocket is not connected. Unable to send data.", EventLogEntryType.Error);
            }
        }

        private void WriteLogEntry(string message, EventLogEntryType entryType)
        {
            eventLog.WriteEntry(message, entryType);
        }
    }
}
