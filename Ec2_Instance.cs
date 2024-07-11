

using Amazon;
using Amazon.CloudWatch;
using Amazon.CloudWatch.Model;
using Amazon.EC2;
using Amazon.EC2.Model;
using Microsoft.AzureMigrate.Appliance.ClientOperationsSDK;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Diagnostics.Metrics;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using Dimension = Amazon.CloudWatch.Model.Dimension;
using WMICounterNames = Microsoft.AzureMigrate.Appliance.ClientOperationsSDK.CimClientConstants.WindowsServerCounterName;
using WMIClassName = Microsoft.AzureMigrate.Appliance.ClientOperationsSDK.CimClientConstants.WindowsServerCounterClassName;
using System.Reflection;
using System.Security.Cryptography;
namespace ArcForPublicCloud
{
    public static class Ec2_Instance
    {
        private static readonly TimeSpan delayForPagination = TimeSpan.FromMilliseconds(50);
        private const int MAX_RESULTS_IN_PAGE = 50;
        private const string HCRPResourceType = "microsoft.hybridcompute/machines";
        private const string HCRPApiVersion = "2023-03-15-preview";
        private const string EC2InstanceName = "EC2Instance";
        private const string CollectorAgentId = "2cbb67c8-0dd4-4767-a6e7-3b6d842ad517";
        
        public static async Task<List<PublicCloudARMResourceModel>> GetAllResources(
            AwsCredentials awsCreds,
            string awsAccountId,
            string publicCloudConnectorArmId,
            string azureRegion)
        {
            var instanceIdToPlatformDetailsMapping = new Dictionary<string, string>();
            var region = RegionEndpoint.CACentral1;
            var ec2ClientForRegion = new AmazonEC2Client(awsCreds.AccessKeyId, awsCreds.SecretAccessKey, awsCreds.SessionToken, region);
            var cloudWatchClient = new AmazonCloudWatchClient(awsCreds.AccessKeyId, awsCreds.SecretAccessKey, awsCreds.SessionToken, region);
            return await GetEC2InstancesRegionAsync(ec2ClientForRegion, cloudWatchClient, region, awsAccountId, publicCloudConnectorArmId, azureRegion, instanceIdToPlatformDetailsMapping);
        }
        public static async Task<Dictionary<string, object>> GetPerformanceDataForCPUAsync(
    AmazonCloudWatchClient cloudWatchClient,
    string instanceId,
    DateTime timestamp,List<Dictionary<string,object>>windowsPerformanceData)
        {
            var processorCounterMap = WindowsUtils
                .WindowsPerfCounterMap()[WMIClassName.Win32_PerfFormattedData_PerfOS_Processor];
            var cpuDetails = new Dictionary<string, object>();
            
            foreach (var metricName in processorCounterMap)
            {
                string counterName = Enum.GetName(typeof(WMICounterNames), metricName.CounterName);
                var metricValue = await GetEC2MetricAsyncForCPU(cloudWatchClient, instanceId, counterName, "CWAgent", timestamp);
                //var metricValue = await GetEC2MetricAsync(cloudWatchClient, instanceId, counterName);
                var value = Convert.ToString(metricValue);

                if (!double.TryParse(value, out double parsedValue))
                {
                    Console.WriteLine($"Unable to parse '{metricName}' with value: {value}.");
                    continue;
                }

                double cpuUtilization = 100 - parsedValue;
                var cpuUtilPerf = WindowsUtils.CreatePerformanceDataStructure(
                    timestamp: timestamp,
                    value: cpuUtilization,
                machineId:instanceId,
                    counterId:CollectorAgentId,
                    counterName: nameof(CimClientConstants.WindowsServerCounterName.cpu_time_idle),
                    instance: instanceId
                );
                windowsPerformanceData.Add(cpuUtilPerf);
            }

            return cpuDetails;
        }

        public static async Task<List<Dictionary<string, object>>> GetPerformanceDataForMemoryAsync(
            AmazonCloudWatchClient cloudWatchClient,
            string instanceId,
            DateTime timestamp, List<Dictionary<string, object>> windowsPerformanceData)
        {
            var memoryCounterMap = WindowsUtils
                .WindowsPerfCounterMap()[WMIClassName.Win32_PerfFormattedData_PerfOS_Memory];
            var memoryDetails = new List<Dictionary<string, object>>();

            foreach (var metricName in memoryCounterMap)
            {
                string counterName = Enum.GetName(typeof(WMICounterNames), metricName.CounterName);
                var metricValue = await GetEC2MetricAsyncForMemory(cloudWatchClient, instanceId, counterName, "CWAgent", timestamp);
                //var metricValue = await GetEC2MetricAsync(cloudWatchClient, instanceId, counterName);
                var value = Convert.ToString(metricValue);

                if (!double.TryParse(value, out double parsedValue))
                {
                    Console.WriteLine($"Unable to parse '{metricName}' with value: {value}.");
                    continue;
                }

                double memoryUtilPercentage = parsedValue;
                var counterData = WindowsUtils.CreatePerformanceDataStructure(
                    timestamp: timestamp,
                    counterName: counterName,machineId:instanceId, counterId: CollectorAgentId,
                    value: Math.Round(memoryUtilPercentage, WindowsUtils.DigitsAfterDecimal),
                    instance: ""
                );

                windowsPerformanceData.Add(counterData);
            }

            return memoryDetails;
        }

        public static async Task<Dictionary<string, object>> GetPerformanceDataForNetworkAsync(
            AmazonCloudWatchClient cloudWatchClient,
            string instanceId,
            DateTime timestamp, List<Dictionary<string, object>> windowsPerformanceData)
        {
            var networkCounterMap = WindowsUtils
                .WindowsPerfCounterMap()[WMIClassName.Win32_PerfFormattedData_Tcpip_NetworkInterface];
            var networkDetails = new Dictionary<string, object>();

            foreach (var counter in networkCounterMap)
            {
                string counterName = Enum.GetName(typeof(WMICounterNames), counter.CounterName);
                var metricValue = await GetEC2MetricAsyncForNetwork(cloudWatchClient, instanceId, counterName, "CWAgent", timestamp);
                //var metricValue = await GetEC2MetricAsync(cloudWatchClient, instanceId, counterName);
                var value = Convert.ToString(metricValue);

                if (!double.TryParse(value, out double parsedValue))
                {
                    Console.WriteLine($"Unable to parse '{counterName}' with value: {value}.");
                    continue;
                }

                var counterData = WindowsUtils.CreatePerformanceDataStructure(
                    timestamp: timestamp,
                    value: Math.Round(parsedValue / counter.UnitConversionFactor, WindowsUtils.DigitsAfterDecimal),
                    machineId:instanceId, counterId: CollectorAgentId,
                    counterName: counterName,
                    instance: ""
                );

                windowsPerformanceData.Add(counterData);
            }

            return networkDetails;
        }
       

        public static async Task<Dictionary<string, object>> GetPerformanceDataForDiskAsync(
            AmazonCloudWatchClient cloudWatchClient,
            string instanceId,
            DateTime timestamp,string instancetype,string deviceName,string imageId,List<Dictionary<string,object>>windowsPerformanceData)
        {
            var diskCounterMap = WindowsUtils
                .WindowsPerfCounterMap()[WMIClassName.Win32_PerfFormattedData_PerfDisk_PhysicalDisk];
            string machineId = instanceId;
            string cid = CollectorAgentId;
            var diskDetails =new Dictionary<string, object>();
            foreach (var metricName in diskCounterMap)
            {
               
                string counterName = Enum.GetName(typeof(WMICounterNames), metricName.CounterName);
                
                var metricValue = await GetEC2MetricAsyncForDisk(cloudWatchClient, instanceId, counterName, "CWAgent",timestamp,instancetype,"xvda1",imageId);
                var value = Convert.ToString(metricValue);

                if (!double.TryParse(value, out double parsedValue))
                {
                    Console.WriteLine($"Unable to parse '{metricName}' with value: {value}.");
                    continue;
                }

                var counterData = WindowsUtils.CreatePerformanceDataStructure(
                    timestamp: timestamp,
                    value: Math.Round(parsedValue / metricName.UnitConversionFactor, WindowsUtils.DigitsAfterDecimal),
                    counterName: counterName,
                    machineId:machineId,
                    counterId:cid,
                    instance: "xvda"
                );

                windowsPerformanceData.Add(counterData);

            }

            return diskDetails;
        }
       
        private static async Task<double> GetEC2MetricAsyncForDisk(
             AmazonCloudWatchClient cloudWatchClient,
             string instanceId,
             string metricName,
             string namespaceName, DateTime timestamp, string instanceType, string deviceName, string imageId,
             int period = 300,
             string statistic = "Sum")
         {
            
             var startTime = new DateTime(timestamp.Year, timestamp.Month, timestamp.Day - 1, 1, 15, 0, DateTimeKind.Utc);
             var endTime = new DateTime(timestamp.Year, timestamp.Month, timestamp.Day, 6, 50, 0, DateTimeKind.Utc);
             var request = new Amazon.CloudWatch.Model.GetMetricStatisticsRequest() 


             {
                 Namespace = namespaceName,
                 MetricName = metricName,
                Dimensions = new List<Dimension>
         {
             new Dimension
             {
                 Name = "InstanceId",
                 Value = instanceId
             },
             new Dimension
             {
                 Name = "ImageId",
                 Value = imageId
             },
            new Dimension
             {
                 Name = "InstanceType",
                 Value = instanceType
             },
             new Dimension
             {
                 Name = "name",
                 Value = deviceName
             }

         },
                 StartTime = startTime,  // Adjust the time range as needed
                 EndTime = DateTime.UtcNow,
                 Period = period,
                 Statistics = new List<string> { statistic }
             };


             var response = await cloudWatchClient.GetMetricStatisticsAsync(request);
             Console.WriteLine($"Metric: {metricName}, InstanceId: {instanceId}, Namespace: {namespaceName}");
             Console.WriteLine($"Request StartTime: {startTime}, EndTime: {endTime}, Period: {period}, Statistic: {statistic}");
             Console.WriteLine($"Number of DataPoints: {response.Datapoints.Count}");
             if (response.Datapoints != null && response.Datapoints.Count > 0)
             {
                 // Return the average value of the latest data point
                 var latestDataPoint = response.Datapoints.OrderByDescending(dp => dp.Timestamp).FirstOrDefault();
                 if (latestDataPoint != null)
                 {
                     return latestDataPoint.Sum;
                 }
             }

             return 0;
         }
        private static async Task<double> GetEC2MetricAsyncForMemory(
             AmazonCloudWatchClient cloudWatchClient,
             string instanceId,
             string metricName,
             string namespaceName, DateTime timestamp,
             int period = 300,
             string statistic = "Average")
        {

            var startTime = new DateTime(timestamp.Year, timestamp.Month, timestamp.Day - 1, 1, 15, 0, DateTimeKind.Utc);
            var endTime = new DateTime(timestamp.Year, timestamp.Month, timestamp.Day, 6, 50, 0, DateTimeKind.Utc);
            var request = new Amazon.CloudWatch.Model.GetMetricStatisticsRequest()


            {
                Namespace = namespaceName,
                MetricName = metricName,
                Dimensions = new List<Dimension>
         {
             new Dimension
             {
                 Name = "InstanceId",
                 Value = instanceId
             },
             
         },
                StartTime = DateTime.UtcNow.AddHours(-24),  // Adjust the time range as needed
                EndTime = DateTime.UtcNow,
                Period = period,
                Statistics = new List<string> { statistic }
            };


            var response = await cloudWatchClient.GetMetricStatisticsAsync(request);
            Console.WriteLine($"Metric: {metricName}, InstanceId: {instanceId}, Namespace: {namespaceName}");
            Console.WriteLine($"Request StartTime: {startTime}, EndTime: {endTime}, Period: {period}, Statistic: {statistic}");
            Console.WriteLine($"Number of DataPoints: {response.Datapoints.Count}");
            if (response.Datapoints != null && response.Datapoints.Count > 0)
            {
                // Return the average value of the latest data point
                var latestDataPoint = response.Datapoints.OrderByDescending(dp => dp.Timestamp).FirstOrDefault();
                if (latestDataPoint != null)
                {
                    return latestDataPoint.Average;
                }
            }

            return 0;
        }
        private static async Task<double> GetEC2MetricAsyncForCPU(
             AmazonCloudWatchClient cloudWatchClient,
             string instanceId,
             string metricName,
             string namespaceName, DateTime timestamp,
             int period = 300,
             string statistic = "Average")
        {

            //var startTime = new DateTime(timestamp.Year, timestamp.Month, timestamp.Day - 1, 1, 15, 0, DateTimeKind.Utc);
            //var endTime = new DateTime(timestamp.Year, timestamp.Month, timestamp.Day, 6, 50, 0, DateTimeKind.Utc);
            var startTime = timestamp.AddHours(-24); // Adjust to 24 hours before the provided timestamp
            var endTime = timestamp;
            var request = new Amazon.CloudWatch.Model.GetMetricStatisticsRequest()


            {
                Namespace = namespaceName,
                MetricName = metricName,
                Dimensions = new List<Dimension>
         {
             new Dimension
             {
                 Name = "InstanceId",
                 Value = instanceId
             },
             new Dimension
             {
                 Name = "cpu",
                 Value ="cpu-total"
             },
            

         },
                StartTime = DateTime.UtcNow.AddHours(-24),  // Adjust the time range as needed
                EndTime = DateTime.UtcNow,
                Period = period,
                Statistics = new List<string> { statistic }
            };


            var response = await cloudWatchClient.GetMetricStatisticsAsync(request);
            Console.WriteLine($"Metric: {metricName}, InstanceId: {instanceId}, Namespace: {namespaceName}");
            Console.WriteLine($"Request StartTime: {startTime}, EndTime: {endTime}, Period: {period}, Statistic: {statistic}");
            Console.WriteLine($"Number of DataPoints: {response.Datapoints.Count}");
            if (response.Datapoints != null && response.Datapoints.Count > 0)
            {
                // Return the average value of the latest data point
                var latestDataPoint = response.Datapoints.OrderByDescending(dp => dp.Timestamp).FirstOrDefault();
                /*if (latestDataPoint != null)
                {
                    return latestDataPoint.Average;
                }
                */
                var cpuTimeIdleSeconds = latestDataPoint.Average / 100.0;

                // Calculate total time duration in seconds
                var totalTimeSeconds = (endTime - startTime).TotalSeconds;

                // Calculate CPU idle percentage
                var cpuIdlePercentage = (1 - cpuTimeIdleSeconds / totalTimeSeconds) * 100;
                return cpuIdlePercentage;
            }

            return 0;
        }
        private static async Task<double> GetEC2MetricAsyncForNetwork(
             AmazonCloudWatchClient cloudWatchClient,
             string instanceId,
             string metricName,
             string namespaceName, DateTime timestamp,
             int period = 300,
             string statistic = "Average")
        {

            var startTime = new DateTime(timestamp.Year, timestamp.Month, timestamp.Day - 2, 1, 15, 0, DateTimeKind.Utc);
            var endTime = new DateTime(timestamp.Year, timestamp.Month, timestamp.Day, 6, 50, 0, DateTimeKind.Utc);
            var request = new Amazon.CloudWatch.Model.GetMetricStatisticsRequest()


            {
                Namespace = namespaceName,
                MetricName = metricName,
                Dimensions = new List<Dimension>
         {
             new Dimension
             {
                 Name = "InstanceId",
                 Value = instanceId
             },
            
             new Dimension
             {
                 Name = "interface",
                 Value = "eth0"
             },

         },
                StartTime = startTime,  // Adjust the time range as needed
                EndTime = DateTime.UtcNow,
                Period = period,
                Statistics = new List<string> { statistic }
            };


            var response = await cloudWatchClient.GetMetricStatisticsAsync(request);
            Console.WriteLine($"Metric: {metricName}, InstanceId: {instanceId}, Namespace: {namespaceName}");
            Console.WriteLine($"Request StartTime: {startTime}, EndTime: {endTime}, Period: {period}, Statistic: {statistic}");
            Console.WriteLine($"Number of DataPoints: {response.Datapoints.Count}");
            if (response.Datapoints != null && response.Datapoints.Count > 0)
            {
                // Return the average value of the latest data point
                var latestDataPoint = response.Datapoints.OrderByDescending(dp => dp.Timestamp).FirstOrDefault();
                if (latestDataPoint != null)
                {
                    return latestDataPoint.Average;
                }
            }

            return 0;
        }
        
        private static async Task<List<PublicCloudARMResourceModel>> GetEC2InstancesRegionAsync(
    AmazonEC2Client ec2Client,
    AmazonCloudWatchClient cloudWatchClient,
    RegionEndpoint region,
    string awsAccountId,
    string publicCloudConnectorId,
    string azureRegion,
    Dictionary<string, string> instanceIdToPlatformDetailsMapping)
        {
            var methodName = GetAsyncMethodName();
            Console.WriteLine($"{methodName}: Discovering EC2s in AWS region: {region} in awsAccountId: {awsAccountId}");

            var instances = new List<PublicCloudARMResourceModel>();
            var describeInstancesRequest = new DescribeInstancesRequest
            {
                MaxResults = MAX_RESULTS_IN_PAGE
            };

            // Fetch instance type memory details
            var instanceTypeMemoryMap = await GetInstanceTypesWithMemoryAsync(ec2Client);

            try
            {
                do
                {
                    var describeInstancesResponse = await ec2Client.DescribeInstancesAsync(describeInstancesRequest);
                    foreach (var reservation in describeInstancesResponse.Reservations)
                    {
                        foreach (var instance in reservation.Instances)
                        {
                            var instanceId = instance.InstanceId;
                            var arn = $"arn:aws:ec2:{region.SystemName}:{awsAccountId}:instance/{instanceId}";
                            var timestamp = DateTime.UtcNow;

                            // Get Disk Performance Data
                            var instanceType = instance.InstanceType;
                            var deviceName = instance.BlockDeviceMappings.FirstOrDefault()?.DeviceName ?? "Unknown";
                            var  windowsPerformanceData = new List<Dictionary<string, object>>();
        var imageId=instance.ImageId;
                           var diskPerfData = await GetPerformanceDataForDiskAsync(cloudWatchClient, instanceId, timestamp,instanceType,deviceName,imageId,windowsPerformanceData);
                           var cpuPerfData = await GetPerformanceDataForCPUAsync(cloudWatchClient, instanceId, timestamp,windowsPerformanceData);
                            var memoryPerfData=await GetPerformanceDataForNetworkAsync(cloudWatchClient, instanceId, timestamp,windowsPerformanceData);    
                           var networkPerfData=await GetPerformanceDataForNetworkAsync(cloudWatchClient,instanceId,timestamp, windowsPerformanceData);
                             //var diskData2=await GetPerfData(cloudWatchClient, instanceId, timestamp);
                            // Get instance type memory details
                            
                           // windowsPerformanceData.Add(diskPerfData);
                            //windowsPerformanceData.Add(cpuPerfData);
                            //windowsPerformanceData.Add(memoryPerfData);
                            //windowsPerformanceData.Add(networkPerfData);
                            string incompletePerfData =
                   JsonConvert.SerializeObject(windowsPerformanceData);
                            List<Microsoft.Azure.Migrate.MigrationService.AgentDataContract.PerformanceData> perfData =
                                JsonConvert.DeserializeObject<List<Microsoft.Azure.Migrate.MigrationService.AgentDataContract.PerformanceData>>(incompletePerfData);
                            var memory = instanceTypeMemoryMap.ContainsKey(instanceType) ? instanceTypeMemoryMap[instanceType] : "Unknown";

                            var properties = JsonUtils.CreateJObject(
                                publicCloudConnectorId,
                                awsAccountId,
                                arn,
                                region.SystemName,
                                instance.InstanceId,
                                Constants.ServiceModelSchemaTypeName,
                                instance,
                                instance.Tags.ToDictionary(tag => tag.Key, tag => tag.Value));

                            // Add Disk Performance Data
                            // properties["DiskPerformanceData"] = diskPerfData; // Uncomment and implement this line

                            // Add memory information to properties
                            properties["Memory"] = memory;

                            try
                            {
                                var ec2Instance = new PublicCloudARMResourceModel
                                {
                                    AzureLocation = azureRegion,
                                    AzureResourceName = instance.InstanceId,
                                    Properties = JsonConvert.DeserializeObject<Dictionary<string, JToken>>(properties.ToString()),
                                };
                                instances.Add(ec2Instance);
                            }
                            catch (Exception ex)
                            {
                                Console.WriteLine($"{methodName}: Error occurred while deserializing EC2 instance {instance.InstanceId} in region {region} for AWS account {awsAccountId} exceptionMessage: {ex.Message} exception: {ex}");
                            }
                        }
                    }
                    describeInstancesRequest.NextToken = describeInstancesResponse.NextToken;

                    // Added a delay to avoid throttling while calling AWS SDK.
                    await Task.Delay(delayForPagination);
                } while (!string.IsNullOrEmpty(describeInstancesRequest.NextToken));
            }
            catch (Exception ex)
            {
                Console.WriteLine($"{methodName}: Error occurred while retrieving EC2 instances. Region: {region}, AwsAccount: {awsAccountId}, " +
                    $"PublicCloudConnector: {publicCloudConnectorId}, exceptionMessage: {ex.Message} exception: {ex}");
            }
            Console.WriteLine($"{methodName}: Finished scanning EC2s. Region: {region}, AccountId: {awsAccountId}, Ec2Count: {instances.Count}");
            return instances;
        }

        static async Task<Dictionary<string, string>> GetInstanceTypesWithMemoryAsync(AmazonEC2Client ec2Client)
        {
            var instanceTypeMemoryMap = new Dictionary<string, string>();
            string nextToken = null;

            do
            {
                var request = new DescribeInstanceTypesRequest
                {
                    NextToken = nextToken
                };

                var response = await ec2Client.DescribeInstanceTypesAsync(request);

                foreach (var instanceType in response.InstanceTypes)
                {
                    var memoryInfo = instanceType.MemoryInfo;
                    if (memoryInfo != null)
                    {
                        var memoryInGib = memoryInfo.SizeInMiB / 1024.0;
                        instanceTypeMemoryMap[instanceType.InstanceType] = $"{memoryInGib} GiB";
                    }
                }

                nextToken = response.NextToken;

            } while (!string.IsNullOrEmpty(nextToken));

            return instanceTypeMemoryMap;
        }

        
        private static string GetAsyncMethodName([CallerMemberName] string name = null) => name;

        private static string GetClassName()
        {
            return nameof(Ec2_Instance);
        }
    }
}
