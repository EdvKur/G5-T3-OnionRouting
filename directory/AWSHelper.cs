using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Amazon.S3;
using Amazon.Runtime;
using Amazon.EC2;
using Amazon.EC2.Model;
using Amazon;


namespace OnionRouting
{
    class AWSHelper
    {
        string credentialProfile;
        string credentialLocation;
//		const string CREDENTIAL_LOCATION = "/home/sunflare/.aws/credentials";
        string chainNodeImageId;
        string chainNodeTag;
        string instanceType;
        string keyPairName;
        string securityGroup;

		private static AWSHelper singletonInstance = null;
		private static Object creationMutex = new Object();

		public static AWSHelper instance()
		{
			lock (creationMutex)
			{
				if (singletonInstance == null)
					singletonInstance = new AWSHelper();
			}

			return singletonInstance;
		}

		private AmazonEC2Client client;
        
		private AWSHelper()
        {
            credentialProfile = Properties.Settings.Default.credentialsProfile;
            credentialLocation = Properties.Settings.Default.credentialLocation;
            chainNodeImageId = Properties.Settings.Default.chainNodeImageId;
            chainNodeTag = Properties.Settings.Default.chainNodeTag;
            instanceType = Properties.Settings.Default.instanceType;
            keyPairName = Properties.Settings.Default.keyPairName;
            securityGroup = Properties.Settings.Default.securityGroup;

            try
            {
                StoredProfileAWSCredentials credentials = new StoredProfileAWSCredentials(credentialProfile, credentialLocation);
                client = new AmazonEC2Client(credentials, Amazon.RegionEndpoint.EUCentral1);
            }
            catch (ArgumentException e)
            {
        
                Log.error("Could not find the credentials at the specified location: {0}", credentialLocation);
                Console.WriteLine("Press enter to exit...");
                Console.ReadLine();
                System.Environment.Exit(1);
            }
            
        }
        
        public List<ChainNodeInfo> discoverChainNodes()
        {
            List<ChainNodeInfo> runningChainNodes = new List<ChainNodeInfo>();

            DescribeInstancesRequest ec2Request = new DescribeInstancesRequest();
            DescribeInstancesResponse ec2Response = client.DescribeInstances(ec2Request);

            foreach (Reservation reservation in ec2Response.Reservations)
                foreach (Instance instance in reservation.Instances)
                {
                    // Amazon region, node name, DNS and IP information
                    if (instance.State.Name == "running" && instance.Tags.Count > 0 && instance.Tags[0].Value == chainNodeTag)
                    {
                        runningChainNodes.Add(new ChainNodeInfo(
                                instance.InstanceId,
                                instance.PublicIpAddress,
                                instance.PublicDnsName,
                                instance.Placement.AvailabilityZone
                            ));
                    }
                }

            return runningChainNodes;
        }

        public string checkChainNodeState(ChainNodeInfo chainNode)
        {
            var request = new DescribeInstancesRequest();
            request.InstanceIds = new List<string> { chainNode.InstanceId };

            var statusResponse = client.DescribeInstances(request);
            Instance instance = statusResponse.Reservations[0].Instances[0];

         
            chainNode.IP = instance.PublicIpAddress;
            chainNode.DNS = instance.PublicDnsName;
            chainNode.Region = instance.Placement.AvailabilityZone;

            return instance.State.Name;
        }

        public ChainNodeInfo launchNewChainNodeInstance()
        {
			RunInstancesRequest request = new RunInstancesRequest() {
				ImageId = chainNodeImageId,
				InstanceType = instanceType,
				MinCount = 1,
				MaxCount = 1,
				KeyName = keyPairName,
				SecurityGroupIds = new List<string>() { securityGroup }
            };

			List<string> instanceIds = new List<string>();
			var launchResponse = client.RunInstances(request);
			Instance instance = launchResponse.Reservation.Instances[0];
            
			var createTagRequest = new CreateTagsRequest();
            createTagRequest.Resources.Add(instance.InstanceId);
            createTagRequest.Tags.Add(new Tag { Key = "Name", Value = chainNodeTag});
			client.CreateTags(createTagRequest);

            return new ChainNodeInfo(
                instance.InstanceId,
                instance.PublicIpAddress,
                instance.PublicDnsName,
                instance.Placement.AvailabilityZone
            );
        }

        public void terminatChainNodeInstance(ChainNodeInfo chainNode)
		{
			var stopRequest = new TerminateInstancesRequest() {
				InstanceIds = new List<string>() { chainNode.InstanceId }
			};
			var stopResponse = client.TerminateInstances(stopRequest);
        }
    }
}
