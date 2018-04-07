using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Amazon.EC2;
using Amazon.EC2.Model;
using Amazon.SimpleSystemsManagement;
using Amazon.SimpleSystemsManagement.Model;
using Amazon.Util;

namespace AWSBoot.Boot
{
    public class AWSEnvironmentService
    {
        private readonly IAmazonEC2 EC2;

        private readonly IAmazonSimpleSystemsManagement SimpleSystemsManagement;

        private static readonly object InstanceIdPadlock = new Object();

        private static readonly SemaphoreSlim TagsPadlock = new SemaphoreSlim(1, 1);

        private static string _InstanceId = null;

        private static bool InstanceIdInitialised = false;

        private IList<Amazon.EC2.Model.Tag> Tags = null;

        public AWSEnvironmentService(IAmazonEC2 ec2, IAmazonSimpleSystemsManagement simpleSystemsManagement)
        {
            EC2 = ec2;

            SimpleSystemsManagement = simpleSystemsManagement;
        }

        public static bool IsEC2Instance()
        {
            return !String.IsNullOrWhiteSpace(InstanceId());
        }

        public static string InstanceId()
        {
            if (!InstanceIdInitialised)
            {
                lock (InstanceIdPadlock)
                {
                    // Double check InstanceId as it might have been changed
                    if (!InstanceIdInitialised)
                    {
                        _InstanceId = EC2InstanceMetadata.InstanceId;

                        Console.WriteLine("InstanceId: " + _InstanceId);
                    }
                }
            }

            return _InstanceId;
        }

        public async Task<bool> HasTag(string key, string value)
        {
            return (await GetInstanceTags()).Any(t => String.Compare(t.Key, key, true) == 0 && String.Compare(t.Value, value, true) == 0);
        }

        public async Task<Amazon.EC2.Model.Tag> GetTag(string key)
        {
            return (await GetInstanceTags()).FirstOrDefault(t => String.Compare(t.Key, key, true) == 0);
        }

        public async Task<string> GetTagValue(string key)
        {
            var tag = (await GetInstanceTags()).FirstOrDefault(t => String.Compare(t.Key, key, true) == 0);

            return tag != null ? tag.Value : null;
        }

        public async Task<IDictionary<string, string>> GetParameters(string path)
        {
            var request = new GetParametersByPathRequest()
            {
                Path = path,
                WithDecryption = true,
                Recursive = true
            };

            var response = await SimpleSystemsManagement.GetParametersByPathAsync(request);

            return response.Parameters.ToDictionary(p => p.Name, p => p.Value);
        }

        public async Task<IList<Amazon.EC2.Model.Tag>> GetInstanceTags()
        {
            if (Tags == null)
            {
                await TagsPadlock.WaitAsync();
                try
                {
                    // Double check
                    if (Tags == null)
                    {
                        var id = InstanceId();

                        if (id != null)
                        {
                            var request = new DescribeInstancesRequest();

                            request.InstanceIds = new List<string>() { id };

                            var response = await EC2.DescribeInstancesAsync(request);

                            var instance = response.Reservations.SelectMany(r => r.Instances).FirstOrDefault();

                            if (instance != null && instance.Tags != null)
                            {
                                Tags = instance.Tags;
                            }
                        }
                    }
                }
                finally
                {
                    TagsPadlock.Release();
                }
            }

            return Tags;
        }
    }
}