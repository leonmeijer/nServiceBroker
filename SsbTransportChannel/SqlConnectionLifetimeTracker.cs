//Copyright (c) Microsoft Corporation.  All rights reserved.
using System;
using System.Collections.Generic;
using System.Text;
using System.Data.SqlClient;
using System.Data;
using System.Diagnostics;

namespace Microsoft.Samples.SsbTransportChannel
{

  /// <summary>
  /// The SqlConnectionLifetimeTracker is used to monitor leaked SqlConnection objects.  After you attach one to a SqlConnection
  /// you will get a notification if that SqlConnection is subsequently leaked, along with the call stack of the code that originally
  /// opened the connection.  If the SqlConnection is properly closed or Disposed by the client code then an event handler in SqlConenctionLifetimeTracker
  /// will supress finalization and detach itself from the SqlConnection.
  /// 
  /// The SqlConnectionLifetimeTracker is can be attached to a SqlConnection and will be reachable from the SqlConnection and 
  /// SqlConnection will be reachable form the SqlConnectionLifetimeTracker.  Therefore the two objects will be Garbage Collected
  /// in the same generation and placed on the FReachable queue at the same time.  We use the Finalizer of SqlConnectionLifetimeTracker
  /// as a proxy to track instances of SqlConnection that are allowed to go to finalization without being closed first (aka leaked).
  /// 
  /// To track the lifetime of a SqlConnection call SqlConnectionLifetimeTracker.TrackSqlConnectionLifetime passing the connection
  /// to be tracked.
  /// 
  /// The finalizer of this type writes a trace message or throws.
  /// This type could be made generic by providing a delegate type to register a callback in case of SqlConnection leaking.
  /// </summary>
    internal class SqlConnectionLifetimeTracker : IDisposable
    {
        SqlConnection con;
        StateChangeEventHandler stateChangeListener;
        StackTrace responsibleStack;

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Performance", "CA1804:RemoveUnusedLocals", MessageId = "lt")]
        public static void TrackSqlConnectionLifetime(SqlConnection con)
        {
            SqlConnectionLifetimeTracker lt = new SqlConnectionLifetimeTracker(con);
        }

        private SqlConnectionLifetimeTracker(SqlConnection sqlConnection)
        {
            this.con = sqlConnection;
            stateChangeListener = new System.Data.StateChangeEventHandler(con_StateChange);
            sqlConnection.StateChange += stateChangeListener;
            responsibleStack = new StackTrace(2);
        }

        public void Dispose()
        {
          //Disconnect and float away
          con.StateChange -= stateChangeListener;
          con = null;
          GC.SuppressFinalize(this);
        }

        void con_StateChange(object sender, System.Data.StateChangeEventArgs e)
        {
            if (e.CurrentState == System.Data.ConnectionState.Closed)
            {
              Dispose();
            }
        }
        ~SqlConnectionLifetimeTracker()
        {
            if (!AppDomain.CurrentDomain.IsFinalizingForUnload())
            {
                string msg = "SQLConnection [" + con.ConnectionString + "] not closed by " + responsibleStack;
                //throw new Exception(msg);
                TraceHelper.TraceEvent(System.Diagnostics.TraceEventType.Critical, msg, "SqlConnectionLifetimeTracker finalizer");
            }
        }
        
    }
}
