
/*using Amazon;
using Amazon.CloudWatch;
using Amazon.CloudWatch.Model;
using Amazon.EC2;
using Amazon.EC2.Model;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;

namespace ArcForPublicCloud
{
    public static class Ec2_Instance
    {
        private static readonly TimeSpan delayForPagination = TimeSpan.FromMilliseconds(50);
        private const int MAX_RESULTS_IN_PAGE = 50;
        private const string HCRPResourceType = "microsoft.hybridcompute/machines";
        private const string HCRPApiVersion = "2023-03-15-preview";
        private const string EC2InstanceName = "EC2Instance";

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
            Console.WriteLine($"{methodName}: Discovering Ec2s in AWS region: {region} in awsAccountId: {awsAccountId}");

            var instances = new List<PublicCloudARMResourceModel>();
            var describeInstancesRequest = new DescribeInstancesRequest
            {
                MaxResults = MAX_RESULTS_IN_PAGE
            };

            try
            {
                do
                {
                    var describeInstancesResponse = await ec2Client.DescribeInstancesAsync(describeInstancesRequest);
                    //await GetEc2ImageInformation(describeInstancesResponse, ec2Client, instanceIdToPlatformDetailsMapping);
                    foreach (var reservation in describeInstancesResponse.Reservations)
                    {
                        foreach (var instance in reservation.Instances)
                        {
                            var instanceId = instance.InstanceId;
                            var arn = $"arn:aws:ec2:{region.SystemName}:{awsAccountId}:instance/{instanceId}";

                            var cpuUtilization = await GetEC2MetricAsync(cloudWatchClient, instanceId, "CPUUtilization");
                            var diskReadOps = await GetEC2MetricAsync(cloudWatchClient, instanceId, "DiskReadOps");
                            var diskWriteOps = await GetEC2MetricAsync(cloudWatchClient, instanceId, "DiskWriteOps");

                            var properties = JsonUtils.CreateJObject(
                                publicCloudConnectorId,
                                awsAccountId,
                                arn,
                                region.SystemName,
                                instance.InstanceId,
                                Constants.ServiceModelSchemaTypeName,
                                instance,
                                instance.Tags.ToDictionary(tag => tag.Key, tag => tag.Value));

                            properties["CPUUtilization"] = cpuUtilization;
                            properties["DiskReadOps"] = diskReadOps;
                            properties["DiskWriteOps"] = diskWriteOps;

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
                                Console.WriteLine(($"{methodName}: Error occurred while deserializing EC2 instance {instance.InstanceId} in region {region} for AWS account {awsAccountId} exceptionMessage: {ex.Message} exception: {ex}"));
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
            Console.WriteLine($"{methodName}: Finished scanning Ec2s. Region: {region}, AccountId: {awsAccountId}, Ec2Count: {instances.Count}");
            return instances;
        }

        private static async Task<double> GetEC2MetricAsync(AmazonCloudWatchClient cloudWatchClient, string instanceId, string metricName)
        {
            var request = new Amazon.CloudWatch.Model.GetMetricStatisticsRequest
            {
                Namespace = "AWS/EC2",
                MetricName = metricName,
                Dimensions = new List<Amazon.CloudWatch.Model.Dimension>
                {
                    new Amazon.CloudWatch.Model.Dimension { Name = "InstanceId", Value = instanceId }
                },
                StartTime = DateTime.UtcNow.AddHours(-24), // Retrieve metrics from the past 24 hours
                EndTime = DateTime.UtcNow,
                Period = 3600, // 1 hour
                Statistics = new List<string> { "Average" }
            };

            var response = await cloudWatchClient.GetMetricStatisticsAsync(request);
            var datapoint = response.Datapoints.OrderByDescending(dp => dp.Timestamp).FirstOrDefault();
            return datapoint?.Average ?? 0.0;
        }

        private static string GetAsyncMethodName([CallerMemberName] string name = null) => name;

        private static string GetClassName()
        {
            return nameof(Ec2_Instance);
        }
    }
}
*/

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
namespace ArcForPublicCloud
{
    public static class Ec2_Instance
    {
        private static readonly TimeSpan delayForPagination = TimeSpan.FromMilliseconds(50);
        private const int MAX_RESULTS_IN_PAGE = 50;
        private const string HCRPResourceType = "microsoft.hybridcompute/machines";
        private const string HCRPApiVersion = "2023-03-15-preview";
        private const string EC2InstanceName = "EC2Instance";
        private static readonly IAmazonCloudWatch _amazonCloudWatch;
        
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
        public static async Task<List<Dictionary<string, object>>> GetPerformanceDataForCPUAsync(
    AmazonCloudWatchClient cloudWatchClient,
    string instanceId,
    DateTime timestamp)
        {
            var processorCounterMap = WindowsUtils
                .WindowsPerfCounterMap()[WMIClassName.Win32_PerfFormattedData_PerfOS_Processor];
            var cpuDetails = new List<Dictionary<string, object>>();

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
                    counterName: nameof(CimClientConstants.WindowsServerCounterName.cpu_time_idle),
                    instance: instanceId
                );

                cpuDetails.Add(cpuUtilPerf);
            }

            return cpuDetails;
        }

        public static async Task<List<Dictionary<string, object>>> GetPerformanceDataForMemoryAsync(
            AmazonCloudWatchClient cloudWatchClient,
            string instanceId,
            DateTime timestamp)
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
                    counterName: counterName,
                    value: Math.Round(memoryUtilPercentage, WindowsUtils.DigitsAfterDecimal),
                    instance: instanceId
                );

                memoryDetails.Add(counterData);
            }

            return memoryDetails;
        }

        public static async Task<List<Dictionary<string, object>>> GetPerformanceDataForNetworkAsync(
            AmazonCloudWatchClient cloudWatchClient,
            string instanceId,
            DateTime timestamp)
        {
            var networkCounterMap = WindowsUtils
                .WindowsPerfCounterMap()[WMIClassName.Win32_PerfFormattedData_Tcpip_NetworkInterface];
            var networkDetails = new List<Dictionary<string, object>>();

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
                    counterName: counterName,
                    instance: instanceId
                );

                networkDetails.Add(counterData);
            }

            return networkDetails;
        }
        private static string ExtractDeviceName(string devicePath)
        {
            if (string.IsNullOrEmpty(devicePath))
            {
                return "Unknown";
            }

            var parts = devicePath.Split('/');
            return parts.Length > 0 ? parts[^1] : "Unknown";
        }

        public static async Task<List<Dictionary<string, object>>> GetPerformanceDataForDiskAsync(
            AmazonCloudWatchClient cloudWatchClient,
            string instanceId,
            DateTime timestamp,string instancetype,string deviceName,string imageId)
        {
            var diskCounterMap = WindowsUtils
                .WindowsPerfCounterMap()[WMIClassName.Win32_PerfFormattedData_PerfDisk_PhysicalDisk];
            var diskDetails = new List<Dictionary<string, object>>();

            foreach (var metricName in diskCounterMap)
            {
                string counterName = Enum.GetName(typeof(WMICounterNames), metricName.CounterName);
                
                var metricValue = await GetEC2MetricAsyncForDisk(cloudWatchClient, instanceId, counterName, "CWAgent",timestamp,instancetype,ExtractDeviceName(deviceName),imageId);
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
                    instance: instanceId
                );

                diskDetails.Add(counterData);
            }

            return diskDetails;
        }

       private static async Task<double> GetEC2MetricAsyncForDisk(
             AmazonCloudWatchClient cloudWatchClient,
             string instanceId,
             string metricName,
             string namespaceName, DateTime timestamp, string instanceType, string deviceName, string imageId,
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
                if (latestDataPoint != null)
                {
                    return latestDataPoint.Average;
                }
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
                 Name = "interface",
                 Value ="eth-0"
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
        /*public static async Task<double> GetEC2MetricAsync(
             AmazonCloudWatchClient cloudWatchClient,
             string instanceId,
             string metricName,
             
             int period = 300,
             string statistic = "Average")
        {
            DateTime startTime = DateTime.UtcNow.AddHours(-48); // Adjust the time range as needed
            DateTime endTime = DateTime.UtcNow;
            try
            {
                var request = new GetMetricDataRequest
                {
                    StartTimeUtc = startTime.ToUniversalTime(),
                    EndTimeUtc = endTime.ToUniversalTime(),
                    MetricDataQueries = new List<MetricDataQuery>
                    {
                        new MetricDataQuery
                        {
                            Id = "ec2",
                            MetricStat = new MetricStat
                            {
                                Metric = new Amazon.CloudWatch.Model.Metric
                                {
                                    Namespace = "CWAgent",
                                    MetricName = metricName,
                                    Dimensions = new List<Dimension>
                                    {
                                        new Dimension { Name = "InstanceId", Value = instanceId }
                                    }
                                },
                                Period = period,
                                Stat = statistic
                            }
                        }
                    }
                };

                var response = await cloudWatchClient.GetMetricDataAsync(request);

                if (response.MetricDataResults.Count > 0)
                {
                    var metricData = response.MetricDataResults[0];
                    if (metricData.Values.Count > 0)
                    {
                        // Assuming we are interested in the first value (single value for 'Average' statistic)
                        return metricData.Values[0];
                    }
                }

                return 0;
            }
            catch (AmazonCloudWatchException ex)
            {
                Console.WriteLine($"Error retrieving CloudWatch metric data: {ex.Message}");
                throw;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error: {ex.Message}");
                throw;
            }
        }
    */
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

                            var imageId=instance.ImageId;
                            var diskPerfData = await GetPerformanceDataForDiskAsync(cloudWatchClient, instanceId, timestamp,instanceType,deviceName,imageId);
                            var cpuPerfData = await GetPerformanceDataForCPUAsync(cloudWatchClient, instanceId, timestamp);
                            var memoryPerfData=await GetPerformanceDataForNetworkAsync(cloudWatchClient, instanceId, timestamp);    
                            var networkPerfData=await GetPerformanceDataForNetworkAsync(cloudWatchClient,instanceId,timestamp);

                            // Get instance type memory details
                           
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

        /* private static async Task<List<PublicCloudARMResourceModel>> GetEC2InstancesRegionAsync(
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
                                 var diskPerfData = await GetPerformanceDataForDiskAsync(cloudWatchClient, instanceId, timestamp);

                                 // Similarly, you can call methods for CPU, Memory, Network, etc.
                                 // var cpuPerfData = await GetPerformanceDataForCPUAsync(cloudWatchClient, instanceId, timestamp);
                                 // var memoryPerfData = await GetPerformanceDataForMemoryAsync(cloudWatchClient, instanceId, timestamp);
                                 // var networkPerfData = await GetPerformanceDataForNetworkAsync(cloudWatchClient, instanceId, timestamp);

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

                                 // Similarly, add data for CPU, Memory, Network, etc.

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
             */
        /*private static async Task<List<PublicCloudARMResourceModel>> GetEC2InstancesRegionAsync(
            AmazonEC2Client ec2Client,
            AmazonCloudWatchClient cloudWatchClient,
            RegionEndpoint region,
            string awsAccountId,
            string publicCloudConnectorId,
            string azureRegion,
            Dictionary<string, string> instanceIdToPlatformDetailsMapping)
        {
            var methodName = GetAsyncMethodName();
            Console.WriteLine($"{methodName}: Discovering Ec2s in AWS region: {region} in awsAccountId: {awsAccountId}");

            var instances = new List<PublicCloudARMResourceModel>();
            var describeInstancesRequest = new DescribeInstancesRequest
            {
                MaxResults = MAX_RESULTS_IN_PAGE
            };

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

                            var cpuUtilization = await GetEC2MetricAsync(cloudWatchClient, instanceId, "CPUUtilization", "AWS/EC2");
                            var diskReadOps = await GetEC2MetricAsync(cloudWatchClient, instanceId, "DiskReadOps", "AWS/EC2");
                            var diskWriteOps = await GetEC2MetricAsync(cloudWatchClient, instanceId, "DiskWriteOps", "AWS/EC2");
                            var memoryUtilization = await GetEC2MetricAsync(cloudWatchClient, instanceId, "mem_used_percent", "CWAgent");

                            var properties = JsonUtils.CreateJObject(
                                publicCloudConnectorId,
                                awsAccountId,
                                arn,
                                region.SystemName,
                                instance.InstanceId,
                                Constants.ServiceModelSchemaTypeName,
                                instance,
                                instance.Tags.ToDictionary(tag => tag.Key, tag => tag.Value));

                            properties["CPUUtilization"] = cpuUtilization;
                            properties["DiskReadOps"] = diskReadOps;
                            properties["DiskWriteOps"] = diskWriteOps;
                            properties["MemoryUtilization"] = memoryUtilization;

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
            Console.WriteLine($"{methodName}: Finished scanning Ec2s. Region: {region}, AccountId: {awsAccountId}, Ec2Count: {instances.Count}");
            return instances;
        }

        private static async Task<double> GetEC2MetricAsync(AmazonCloudWatchClient cloudWatchClient, string instanceId, string metricName, string metricNamespace)
        {
            var request = new Amazon.CloudWatch.Model.GetMetricStatisticsRequest
            {
                Namespace = metricNamespace,
                MetricName = metricName,
                Dimensions = new List<Amazon.CloudWatch.Model.Dimension>
                {
                    new Amazon.CloudWatch.Model.Dimension { Name = "InstanceId", Value = instanceId }
                },
                StartTime = DateTime.UtcNow.AddHours(-24), // Retrieve metrics from the past 24 hours
                EndTime = DateTime.UtcNow,
                Period = 3600, // 1 hour
                Statistics = new List<string> { "Average" }
            };

            var response = await cloudWatchClient.GetMetricStatisticsAsync(request);
            var datapoint = response.Datapoints.OrderByDescending(dp => dp.Timestamp).FirstOrDefault();
            return datapoint?.Average ?? 0.0;
        }
        */

        private static string GetAsyncMethodName([CallerMemberName] string name = null) => name;

        private static string GetClassName()
        {
            return nameof(Ec2_Instance);
        }
    }
}
