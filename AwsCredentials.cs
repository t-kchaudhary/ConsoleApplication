namespace ArcForPublicCloud
{
    using System;
    using global::Amazon.SecurityToken.Model;

    /// <summary>
    /// AWS credentials to access AWS resources
    /// </summary>
    public class AwsCredentials
    {
        /// <summary>
        /// AWS access key id
        /// </summary>
        public string AccessKeyId { get; private set; }

        /// <summary>
        /// AWS secret access key
        /// </summary>
        public string SecretAccessKey { get; private set; }

        /// <summary>
        /// AWS session token
        /// </summary>
        public string SessionToken { get; private set; }

        /// <summary>
        /// AWS session token expiration date
        /// </summary>
        public DateTime ExpirationDate { get; private set; }

        /// <summary>
        /// Initializes a new instance of <see cref="AwsCredentials"/> class.
        /// </summary>
        /// <param name="accessKeyId"></param>
        /// <param name="secretAccessKey"></param>
        /// <param name="sessionToken"></param>
        /// <param name="expirationDate"></param>
        public AwsCredentials(string accessKeyId, string secretAccessKey, string sessionToken, DateTime expirationDate)
        {
            this.AccessKeyId = accessKeyId;
            this.SecretAccessKey = secretAccessKey;
            this.SessionToken = sessionToken;
            this.ExpirationDate = expirationDate;
        }

        /// <summary>
        /// Initializes a new instance of <see cref="AwsCredentials"/> class.
        /// </summary>
        /// <param name="credentials"></param>
        public AwsCredentials(Credentials credentials)
        {
            this.AccessKeyId = credentials.AccessKeyId;
            this.SecretAccessKey = credentials.SecretAccessKey;
            this.SessionToken = credentials.SessionToken;
            this.ExpirationDate = credentials.Expiration;
        }
    }
}
