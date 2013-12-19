-- Create Database
create database ssb_db
	WITH TRUSTWORTHY ON;
GO

use ssb_db;
GO

-- Create Master Key
CREATE MASTER KEY 
ENCRYPTION BY PASSWORD = '123456';
GO

-- Create Table Order
CREATE TABLE [dbo].[Orders](
	[OrderID] [uniqueidentifier] NOT NULL,
	[CustomerName] [varchar](50) NOT NULL,
 CONSTRAINT [PK_Orders] PRIMARY KEY CLUSTERED 
(
	[OrderID] ASC
)WITH (PAD_INDEX  = OFF, STATISTICS_NORECOMPUTE  = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS  = ON, ALLOW_PAGE_LOCKS  = ON) ON [PRIMARY]
) ON [PRIMARY]

GO

SET ANSI_PADDING OFF
GO

ALTER TABLE [dbo].[Orders] ADD  CONSTRAINT [DF_Orders_OrderID]  DEFAULT (newid()) FOR [OrderID]
GO


-- Configuring Service Broker
 /****** Object:  MessageType [http://ssbtransport/sample/IOrderService/SubmitOrder]    Script Date: 12/17/2013 15:53:46 ******/
CREATE MESSAGE TYPE [http://ssbtransport/sample/IOrderService/SubmitOrder] AUTHORIZATION [dbo] VALIDATION = WELL_FORMED_XML
GO

/****** Object:  ServiceContract [http://ssbtransport/sample/OrderServiceContract_OneWay]    Script Date: 12/17/2013 15:53:59 ******/
CREATE CONTRACT [http://ssbtransport/sample/OrderServiceContract_OneWay] AUTHORIZATION [dbo] ([http://ssbtransport/sample/IOrderService/SubmitOrder] SENT BY INITIATOR)
GO

/****** Object:  ServiceQueue [dbo].[ClientQueue]    Script Date: 12/17/2013 15:54:13 ******/
CREATE QUEUE [dbo].[ClientQueue] WITH STATUS = ON , RETENTION = OFF  ON [PRIMARY] 
GO

/****** Object:  ServiceQueue [dbo].[ServiceQueue]    Script Date: 12/17/2013 15:54:22 ******/
CREATE QUEUE [dbo].[ServiceQueue] WITH STATUS = ON , RETENTION = OFF  ON [PRIMARY] 
GO

/****** Object:  BrokerService [Client]    Script Date: 12/17/2013 15:54:32 ******/
CREATE SERVICE [Client]  AUTHORIZATION [dbo]  ON QUEUE [dbo].[ClientQueue] ([http://ssbtransport/sample/OrderServiceContract_OneWay])
GO

/****** Object:  BrokerService [Service]    Script Date: 12/17/2013 15:54:42 ******/
CREATE SERVICE [Service]  AUTHORIZATION [dbo]  ON QUEUE [dbo].[ServiceQueue] ([http://ssbtransport/sample/OrderServiceContract_OneWay])
GO


-- Create Order Trigger
CREATE  TRIGGER [dbo].[Trg_Order_Insert]
ON  [dbo].[Orders]
FOR INSERT 
AS 
BEGIN
    SET NOCOUNT ON;
    DECLARE @MessageBody VARCHAR(max),@CustomerName varchar(50),@OrderID varchar(50)
	SELECT @CustomerName = CustomerName,@OrderID = OrderID FROM inserted
	
    SET @MessageBody = 
    '<s:Envelope xmlns:s="http://www.w3.org/2003/05/soap-envelope" xmlns:a="http://www.w3.org/2005/08/addressing">
	  <s:Header>
		<a:Action s:mustUnderstand="1">http://ssbtransport/sample/IOrderService/SubmitOrder</a:Action>
		<a:To s:mustUnderstand="1">net.ssb:source=Client:target=Service</a:To>
	  </s:Header>
	  <s:Body>
		<SubmitOrder xmlns="http://ssbtransport/sample">
		  <order xmlns:i="http://www.w3.org/2001/XMLSchema-instance">
			<CustomerName>'+ @CustomerName +'</CustomerName>
			<OrderId>'+ @OrderID +'</OrderId>
		  </order>
		</SubmitOrder>
	  </s:Body>
	 </s:Envelope>'

    If (@MessageBody IS NOT NULL)  
    BEGIN 
        DECLARE @Handle UNIQUEIDENTIFIER;   
        BEGIN DIALOG CONVERSATION @Handle   
        FROM SERVICE Client   
        TO SERVICE 'Service'   
        ON CONTRACT [http://ssbtransport/sample/OrderServiceContract_OneWay]   
        WITH ENCRYPTION = OFF;   
        SEND ON CONVERSATION @Handle   
        MESSAGE TYPE [http://ssbtransport/sample/IOrderService/SubmitOrder](@MessageBody);
    END
END