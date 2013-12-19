//Copyright (c) Microsoft Corporation.  All rights reserved.
using System;
using System.Collections.Generic;
using System.Text;
using System.ServiceModel;
using System.ServiceModel.Channels;
using System.Data.SqlClient;
using System.Data;
namespace Microsoft.Samples.SsbTransportChannel
{
    delegate Message ReceiveDelegate(TimeSpan timeout);
    delegate void CloseDelegate(TimeSpan timeout);
    delegate void OpenDelegate(TimeSpan timeout);


    public static class SsbHelper
    {

        internal static SqlConnection GetConnection(String connectionString)
        {
            SqlConnection con = new System.Data.SqlClient.SqlConnection(connectionString);
            
            SqlConnectionLifetimeTracker.TrackSqlConnectionLifetime(con);
            return con;
        }
        internal static bool IsValidSsbAddress(Uri uri)
        {
            // valid format is net.ssb://hostname/servicename
            if (uri.Scheme == SsbConstants.Scheme &&
                uri.Port == -1 &&
                !String.IsNullOrEmpty(uri.PathAndQuery) &&
                uri.PathAndQuery != "/")
            {
                return true;
            }
            else
            {
                return false;
            }
        }
        internal static string GetServiceName(Uri uri)
        {
            return new SsbUri(uri).Service;
        }

        internal static ServiceInfo GetServiceInfo(string serviceName, TimeSpan timeout, string connstr)
        {
            using (SqlConnection con = SsbHelper.GetConnection(connstr))
            {
                con.Open();
                return GetServiceInfo(serviceName, timeout, con);
            }
        }

        internal static ServiceInfo GetServiceInfo(string serviceName, TimeSpan timeout, SqlConnection con)
        {
            TimeoutHelper helper=new TimeoutHelper(timeout);
            ServiceInfo info = null;
            string SQL = @"
                select 
                  s.name As ServiceName
                , s.service_id As ServiceId
                , q.name As QueueName
                , q.object_id As QueueId 
                from sys.services s
                inner join sys.service_queues q
                  on s.service_queue_id=q.object_id 
                where s.name=@ServiceName";

            SqlCommand cmd = new SqlCommand(SQL, con);
            cmd.Parameters.Add("@ServiceName", SqlDbType.VarChar).Value = serviceName;
            cmd.CommandTimeout = helper.RemainingTimeInMillisecondsOrZero();

            try
            {
                using (SqlDataReader rdr = cmd.ExecuteReader())
                {
                    if (!rdr.Read())
                    {
                        throw new CommunicationException(string.Format("Service {0} not found", serviceName));
                    }

                    info = new ServiceInfo();
                    info.ServiceName = rdr.GetString(0);
                    info.ServiceId = rdr.GetInt32(1);
                    info.QueueName = rdr.GetString(2);
                    info.QueueId = rdr.GetInt32(3);

                    rdr.Close();
                }
            }
            catch (SqlException ex)
            {
                if (!helper.IsTimeRemaining)
                {
                    throw new TimeoutException(String.Format("Timed out while getting service information. Timeout value was {0} seconds",timeout.TotalSeconds),ex);
                        
                }
                else
                {
                    throw new CommunicationException("An exception occurred while getting service information", ex);
                }
            }

            return info;
        }

        static string forbiddenCharacters = "[]'\"\r\n\t";
        static char[] arrForbiddenCharacters = forbiddenCharacters.ToCharArray();
        /// <summary>
        /// A quick check for SQL escape characters and delimiters.  Identifiers should
        /// follow the rules for SQL Server Regular Identifiers.  In case they don't
        /// the binding will automatically apply the delimiters.  Delimiters embedded in the 
        /// identifier should be avoided as they are confusing and a security risk.  Eg configure
        /// the binding to use contract "My Contract", instead of "[My Contract]".
        /// </summary>
        /// <param name="identifier"></param>
        internal static string ValidateIdentifier(string identifier)
        {
            if (identifier.IndexOfAny(arrForbiddenCharacters) != -1)
            {
                throw new ArgumentException("Identifer contains an illegal character ([]'\"\r\n\t). ");
            }
            return identifier;
        }
        internal static void SetConversationTimer(Guid conversationHandle, TimeSpan timerTimeout, string connstr)
        {
            using (SqlConnection con = SsbHelper.GetConnection(connstr))
            {
                con.Open();
                SetConversationTimer(conversationHandle, timerTimeout, con);
            }
        }
        internal static void SetConversationTimer(Guid conversationHandle, TimeSpan timerTimeout, SqlConnection con)
        {
            double totalSeconds = Math.Round(timerTimeout.TotalSeconds, MidpointRounding.AwayFromZero);

            int timeoutSeconds;

            if (totalSeconds > 2147483646d)
            {
                timeoutSeconds = 2147483646;
            }
            else if (totalSeconds < 1)
            {
                timeoutSeconds = 1;
            }
            else
            {
                timeoutSeconds = (int)totalSeconds;
            }


            string sql = @"BEGIN CONVERSATION TIMER ( @conversation_handle ) TIMEOUT = @timeout ";
            SqlCommand cmd = new SqlCommand(sql, con);
            cmd.Parameters.Add("@conversation_handle", SqlDbType.UniqueIdentifier).Value = conversationHandle;
            cmd.Parameters.Add("@timeout", SqlDbType.Int).Value = timeoutSeconds;
            cmd.ExecuteNonQuery();
        }

        internal static ConversationInfo GetConversationInfo(Guid conversationHandle, string constr)
        {
            using (SqlConnection con = SsbHelper.GetConnection(constr))
            {
                con.Open();
                return GetConversationInfo(conversationHandle, con);
            }
        }
        internal static ConversationInfo GetConversationInfo(Guid conversationHandle, SqlConnection con)
        {
            #region Build SqlCommand cmd
            string SQL = @"
select 
@ConversationGroupId = ce.conversation_group_id,
@ConversationId = ce.conversation_id,
@ServiceName = s.name,
@TargetServiceName = ce.far_service,
@QueueName = q.name,
@State = ce.state
from sys.conversation_endpoints ce
join sys.services s
  on ce.service_id = s.service_id
join sys.service_queue_usages u
  on u.service_id = s.service_id
join sys.service_queues q
  on q.object_id = u.service_queue_id
where ce.conversation_handle = @ConversationHandle;

if @@rowcount = 0
begin
  --raiserror('No Service Broker conversation found with handle: %s',16,1,@ConversationHandle);
  raiserror('Service Broker conversation not found.',16,1);
end

";

            SqlCommand cmd = new SqlCommand(SQL, con);

            SqlParameter pConversationId = cmd.Parameters.Add("@ConversationId", SqlDbType.UniqueIdentifier);
            pConversationId.Direction = ParameterDirection.Output;

            SqlParameter pConversationGroupId = cmd.Parameters.Add("@ConversationGroupId", SqlDbType.UniqueIdentifier);
            pConversationGroupId.Direction = ParameterDirection.Output;

            SqlParameter pServiceName = cmd.Parameters.Add("@ServiceName", SqlDbType.VarChar);
            pServiceName.Size = 30;
            pServiceName.Direction = ParameterDirection.Output;

            SqlParameter pTargetServiceName = cmd.Parameters.Add("@TargetServiceName", SqlDbType.VarChar);
            pTargetServiceName.Size = 30;
            pTargetServiceName.Direction = ParameterDirection.Output;

            SqlParameter pQueueName = cmd.Parameters.Add("@QueueName", SqlDbType.VarChar);
            pQueueName.Size = 30;
            pQueueName.Direction = ParameterDirection.Output;

            SqlParameter pState = cmd.Parameters.Add("@State", SqlDbType.Char);
            pState.Size = 2;
            pState.Direction = ParameterDirection.Output;

            SqlParameter pConversationHandle = cmd.Parameters.Add("@ConversationHandle", SqlDbType.UniqueIdentifier);
            pConversationHandle.Value = conversationHandle;
            #endregion

            try
            {
                cmd.ExecuteNonQuery();
            }
            catch (SqlException ex)
            {
                throw new CommunicationException("An exception occurred while getting service information", ex);
            }
       


            string state = (string)pState.Value;
            if (state != "CO" && state != "SO")
            {
                throw new InvalidOperationException("Cannot open conversation in state "
                  + ConversationInfo.GetTransmissionStateDescription(state));
            }

            string serviceName = (string)pServiceName.Value;
            string targetServiceName = (string)pTargetServiceName.Value;
            string queueName = (string)pQueueName.Value;
            Guid conversationId = (Guid)pConversationId.Value;
            Guid conversationGroupId = (Guid)pConversationGroupId.Value;

            ConversationInfo ci = new ConversationInfo(serviceName, queueName, targetServiceName, conversationId, conversationHandle, conversationGroupId, state);




            return ci;
        }

        public static int EndAllConversationsWithCleanup(string connstr)
        {
            using (SqlConnection con = SsbHelper.GetConnection(connstr))
            {
                con.Open();
                return EndAllConversationsWithCleanup(con);
            }
        }
        internal static int EndAllConversationsWithCleanup(SqlConnection con)
        {

            string SQL = @"
declare conversations cursor local for
select conversation_handle from sys.conversation_endpoints
where not (state='CD' and is_initiator=0)

declare @ConversationHandle uniqueidentifier
declare @count int
set @count = 0

open conversations
fetch next from conversations into @ConversationHandle

while @@fetch_status = 0
begin
  set @count = @count + 1
  end conversation @ConversationHandle with cleanup;
fetch next from conversations into @ConversationHandle
end

select @count conversations_ended
";
            using (SqlCommand cmd = new SqlCommand(SQL, con))
            {
                return (int)cmd.ExecuteScalar();
            }

        }
        
        public static void EndConversationWithCleanup(string connstr, Guid conversationHandle)
        {
            using (SqlConnection con = SsbHelper.GetConnection(connstr))
            {
                con.Open();
                EndConversationWithCleanup(con, conversationHandle);
            }
        }
        internal static void EndConversationWithCleanup(SqlConnection con, Guid conversationHandle)
        {
            string SQL = @"END CONVERSATION @Conversation WITH CLEANUP";
            using (SqlCommand cmd = new SqlCommand(SQL, con))
            {
                SqlParameter pConversation = cmd.Parameters.Add("@Conversation", SqlDbType.UniqueIdentifier);
                pConversation.Value = conversationHandle;

                cmd.ExecuteNonQuery();
            }
        }


        internal static string CheckConversationForErrorMessage(string connstr, Guid conversationHandle)
        {
            using (SqlConnection con = SsbHelper.GetConnection(connstr))
            {
                con.Open();
                return CheckConversationForErrorMessage(con, conversationHandle);
            }
        }
        internal static string  CheckConversationForErrorMessage(SqlConnection con, Guid conversationHandle)
        {
            #region Build SqlCommand cmd

            string SQL = @"

  declare @queueName varchar(200)
  set @queueName =
  (
    select sq.name queue_name
    from sys.conversation_endpoints ce
    join sys.services s
      on s.service_id = ce.service_id
    join sys.service_queue_usages squ
      on s.service_id = squ.service_id
    join sys.service_queues sq
      on squ.service_queue_id = sq.object_id
    where conversation_handle = @Conversation
  )

  declare @sql nvarchar(max)
  declare @msg xml
  set @sql = N'select @m=cast(message_body as xml) from [' + @queueName + '] with (nolock) where message_type_id = 1 and conversation_handle = @ch'
  --print @sql
  exec sp_executesql @sql, N'@ch uniqueidentifier, @m xml output',@ch=@Conversation, @m=@msg out
  
  if @msg is not null
  begin
  
  ;with xmlnamespaces ('http://schemas.microsoft.com/SQL/ServiceBroker/Error' as e)
   select @ErrorMessage = E.Error.value('(e:Code)[1]','nvarchar(max)') + ': ' + E.Error.value('(e:Description)[1]','nvarchar(max)') 
   from @msg.nodes('/e:Error') as E(Error)
 
  end

";
            #endregion

            using (SqlCommand cmd = new SqlCommand(SQL, con))
            {
                SqlParameter pConversation = cmd.Parameters.Add("@Conversation", SqlDbType.UniqueIdentifier);
                pConversation.Value = conversationHandle;
                SqlParameter pErrorMessage = cmd.Parameters.Add("@ErrorMessage", SqlDbType.NVarChar, 500);
                pErrorMessage.Direction = ParameterDirection.Output;

                cmd.ExecuteNonQuery();
                
                string errorMessage = pErrorMessage.Value as string;
                return errorMessage;
            }
        }

    }

}
