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
    static class AWSHelper
    {
        const string CREDENTIAL_LOCATION = "C:\\credentials\\credentials";
        const string CHAIN_NODE_IMAGE_ID = "ami-3005342d";
        const string CHAIN_NODE_TAG = "G5-T3-ChainNode";
        const string INSTANCE_TYPE = "t2.micro";
        const string KEY_PAIR_NAME = "G5-T3-Keypair";
        const string SECURITY_GROUP = "sg-68ff1601"; 

        private static AmazonEC2Client client;
        
        public static void init()
        {
            StoredProfileAWSCredentials credentials = new StoredProfileAWSCredentials("Administrator", CREDENTIAL_LOCATION);
            client = new AmazonEC2Client(credentials, Amazon.RegionEndpoint.EUCentral1);
        }
        
        public static List<ChainNodeInfo> discoverChainNodes()
        {
            List<ChainNodeInfo> runningChainNodes = new List<ChainNodeInfo>();

            DescribeInstancesRequest ec2Request = new DescribeInstancesRequest();
            DescribeInstancesResponse ec2Response = client.DescribeInstances(ec2Request);

            foreach (Reservation reservation in ec2Response.Reservations)
                foreach (Instance instance in reservation.Instances)
                {
                    // Amazon region, node name, DNS and IP information
                    if (instance.State.Name == "running" && instance.Tags.Count > 0 && instance.Tags[0].Value == CHAIN_NODE_TAG)
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

        public static string checkChainNodeState(ChainNodeInfo chainNode)
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

        public static ChainNodeInfo launchNewChainNodeInstance()
        {
			RunInstancesRequest request = new RunInstancesRequest() {
				ImageId = CHAIN_NODE_IMAGE_ID,
				InstanceType = INSTANCE_TYPE,
				MinCount = 1,
				MaxCount = 1,
				KeyName = KEY_PAIR_NAME,
				SecurityGroupIds = new List<string>() { SECURITY_GROUP }
            };

			List<string> instanceIds = new List<string>();
			var launchResponse = client.RunInstances(request);
			Instance instance = launchResponse.Reservation.Instances[0];
            
			var createTagRequest = new CreateTagsRequest();
            createTagRequest.Resources.Add(instance.InstanceId);
            createTagRequest.Tags.Add(new Tag { Key = "Name", Value = CHAIN_NODE_TAG });
			client.CreateTags(createTagRequest);

            return new ChainNodeInfo(
                instance.InstanceId,
                instance.PublicIpAddress,
                instance.PublicDnsName,
                instance.Placement.AvailabilityZone
            );
        }

        public static void terminatChainNodeInstance(ChainNodeInfo chainNode)
		{
			var stopRequest = new TerminateInstancesRequest() {
				InstanceIds = new List<string>() { chainNode.InstanceId }
			};
			var stopResponse = client.TerminateInstances(stopRequest);
        }
    }
}
