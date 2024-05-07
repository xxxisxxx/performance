using System;
using System.Windows.Forms;
using System.ServiceProcess;
using System.IO;

namespace ControlApp
{
    public partial class Form1 : Form
    {
        private readonly string serviceName = "PerformanceMonitorService";
        private readonly ServiceController serviceController;

        public Form1()
        {
            InitializeComponent();
            serviceController = new ServiceController(serviceName);
            UpdateServiceStatus();

            // 检查是否以管理员权限运行
            if (!IsRunningAsAdministrator())
            {
                MessageBox.Show("请以管理员权限重新运行应用程序！", "提示", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                label1.Show();
            }
        }

        private bool IsRunningAsAdministrator()
        {
            return new System.Security.Principal.WindowsPrincipal(System.Security.Principal.WindowsIdentity.GetCurrent())
                       .IsInRole(System.Security.Principal.WindowsBuiltInRole.Administrator);
        }

        private void UpdateServiceStatus()
        {
            try
            {
                serviceController.Refresh();

                // 获取服务状态
                ServiceControllerStatus status = serviceController.Status;

                // 根据状态设置文本和图标
                switch (status)
                {
                    case ServiceControllerStatus.Running:
                        toolStripStatusLabel1.Text = "服务状态：运行中";
                        toolStripStatusLabel1.Image = Properties.Resources.RunningIcon; // 替换为你的运行图标
                        break;
                    case ServiceControllerStatus.Stopped:
                        toolStripStatusLabel1.Text = "服务状态：已停止";
                        toolStripStatusLabel1.Image = Properties.Resources.StoppedIcon; // 替换为你的停止图标
                        break;
                    case ServiceControllerStatus.Paused:
                        toolStripStatusLabel1.Text = "服务状态：已暂停";
                        toolStripStatusLabel1.Image = Properties.Resources.PausedIcon; // 替换为你的暂停图标
                        break;
                    case ServiceControllerStatus.StartPending:
                    case ServiceControllerStatus.StopPending:
                    case ServiceControllerStatus.ContinuePending:
                    case ServiceControllerStatus.PausePending:
                        toolStripStatusLabel1.Text = "服务状态：正在处理中";
                        toolStripStatusLabel1.Image = Properties.Resources.PausedIcon; // 替换为你的处理中图标
                        break;
                }
            }
            catch (Exception ex)
            {
                // 处理异常
                toolStripStatusLabel1.Text = "服务状态：获取失败";
                toolStripStatusLabel1.Image = Properties.Resources.PausedIcon; // 替换为你的错误图标
                Console.WriteLine("获取服务状态失败：" + ex.Message);
            }
        }

        private void 关于ToolStripMenuItem_Click(object sender, EventArgs e)
        {
            MessageBox.Show("献给邓千 2024年5月6日\r\n\r\n别忘了跟我介绍媳妇子");
        }

        private void 卸载服务ToolStripMenuItem_Click(object sender, EventArgs e)
        {
            try
            {
                ServiceHelper.UnregisterService(serviceName);
                UpdateServiceStatus();
                MessageBox.Show("服务已卸载", "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"卸载服务时出错：{ex.Message}", "错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void button1_Click(object sender, EventArgs e)
        {
            try
            {
                string currentDirectory = Directory.GetCurrentDirectory();
                string exePath = Path.Combine(currentDirectory, "PerformanceService.exe");

                if (!File.Exists(exePath))
                {
                    MessageBox.Show("找不到 PerformanceService.exe 文件", "错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return;
                }

                if (!ServiceHelper.IsServiceRegistered(serviceName))
                {
                    ServiceHelper.RegisterService(serviceName, "Performance Monitor Service", "将计算机的性能信息实时推送到eye服务器,便于给ESP32实现真实仪表盘提供实时数据", exePath);
                }

                serviceController.Start();
                serviceController.WaitForStatus(ServiceControllerStatus.Running);
                UpdateServiceStatus();
                MessageBox.Show("服务已启动", "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"启动服务时出错：{ex.Message}", "错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void button2_Click(object sender, EventArgs e)
        {
            try
            {
                serviceController.Stop();
                serviceController.WaitForStatus(ServiceControllerStatus.Stopped);
                UpdateServiceStatus();
                MessageBox.Show("服务已停止", "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"停止服务时出错：{ex.Message}", "错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
    }

    public static class ServiceHelper
    {
        public static bool IsServiceRegistered(string serviceName)
        {
            // 实现检查服务是否已注册的代码
            ServiceController[] services = ServiceController.GetServices();
            foreach (ServiceController service in services)
            {
                if (service.ServiceName == serviceName)
                {
                    return true;
                }
            }
            return false;
        }

        public static void RegisterService(string serviceName, string displayName, string description, string exePath)
        {
            using (var process = new System.Diagnostics.Process())
            {
                process.StartInfo.FileName = "sc";
                process.StartInfo.Arguments = $"create {serviceName} DisplayName= \"{displayName}\" start= auto binPath= \"{exePath}\"";
                process.StartInfo.UseShellExecute = false;
                process.StartInfo.RedirectStandardOutput = true;
                process.Start();
                process.WaitForExit();

                // 如果创建服务成功，再添加描述
                if (process.ExitCode == 0)
                {
                    process.StartInfo.Arguments = $"description {serviceName} \"{description}\"";
                    process.Start();
                    process.WaitForExit();
                }
            }
        }




        public static void UnregisterService(string serviceName)
        {

            using (var process = new System.Diagnostics.Process())
            {
                process.StartInfo.FileName = "sc";
                process.StartInfo.Arguments = $"delete {serviceName}";
                process.StartInfo.UseShellExecute = false;
                process.StartInfo.RedirectStandardOutput = true;
                process.Start();
                process.WaitForExit();
            }
        }
    }
}
