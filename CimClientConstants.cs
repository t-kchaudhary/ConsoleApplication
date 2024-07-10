namespace Microsoft.AzureMigrate.Appliance.ClientOperationsSDK
{
    /// <summary>
    /// Server Constants.
    /// </summary>
    internal class CimClientConstants
    {
        /// <summary>
        /// Names of Windows Counter Classes.
        /// </summary>
        public enum WindowsServerCounterClassName
        {
            /// <summary>
            /// Physical Server Network Configuration Class.
            /// </summary>
            Win32_NetworkAdapterConfiguration,

            /// <summary>
            /// Physical Server Windows Operating System.
            /// </summary>
            Win32_OperatingSystem,

            /// <summary>
            /// Physical Server Windows BIOS details class.
            /// </summary>
            Win32_ComputerSystemProduct,

            /// <summary>
            /// Physical Server Windows details class.
            /// </summary>
            Win32_ComputerSystem,

            /// <summary>
            /// Physical Server Windows Processor details class.
            /// </summary>
            Win32_Processor,

            /// <summary>
            /// Physical Server Windows Disk details class.
            /// </summary>
            Win32_DiskDrive,

            /// <summary>
            /// Physical Server Windows Disk partition class.
            /// </summary>
            Win32_DiskPartition,

            /// <summary>
            /// Physical Disk Performance Data Counter class name.
            /// </summary>
            Win32_PerfFormattedData_PerfDisk_PhysicalDisk,

            /// <summary>
            /// Processor Performance Data Counter class name.
            /// </summary>
            Win32_PerfFormattedData_PerfOS_Processor,

            /// <summary>
            /// Network Interface Performance Data Counter class name.
            /// </summary>
            Win32_PerfFormattedData_Tcpip_NetworkInterface,

            /// <summary>
            /// Memory Details Performance Counter class.
            /// </summary>
            Win32_PerfFormattedData_PerfOS_Memory
        }

        /// <summary>
        /// Windows Server Counter Name for performance data.
        /// </summary>
        public enum WindowsServerCounterName
        {
            /// <summary>
            /// Counter Disk Writes per second to be used for IOPS calculations.
            /// </summary>
            diskio_writes,

            /// <summary>
            /// Counter Disk Write bytes per second to be used for IOPS Calculation.
            /// </summary>
            diskio_write_bytes,

            /// <summary>
            /// Counter Disk reads Per second to be used for IOPS Calculation.
            /// </summary>
            diskio_reads,

            /// <summary>
            /// Counter Disk Read Operations per second to be used for IOPS calculation.
            /// </summary>
            diskio_read_bytes,

            /// <summary>
            /// Counter for Network Reads for throughput calculation.
            /// </summary>
            net_bytes_recv,

            /// <summary>
            /// Counter for network Writes for throughput calculation.
            /// </summary>
            net_bytes_sent,

            /// <summary>
            /// Percentage Idle time of CPU for calculating CPU Utilization.
            /// </summary>
            cpu_time_idle,

            /// <summary>
            /// Available Memory in the system.
            /// </summary>
            mem_used_percent
        }

        /// <summary>
        /// Static class for constants for WMI related calls.
        /// </summary>
        public static class WMIQueryInfo
        {
            /// <summary>
            /// CIM Query Namespace.
            /// </summary>
            public const string CIMQueryNamespace = @"root\cimV2";

            /// <summary>
            /// Windows Query Language for querying data.
            /// </summary>
            public const string WindowsQueryClass = "WQL";
        }

    }
}