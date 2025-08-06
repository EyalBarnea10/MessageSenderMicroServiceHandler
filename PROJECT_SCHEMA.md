# TCP Message Sender Service - Architecture Schema

## 🏗️ System Overview

This is a **production-ready microservice** that implements a scalable TCP stream processor with Kafka integration, built following **SOLID principles** and enterprise patterns.

## 📋 What The Code Does

### 1. **TCP Stream Ingestion** (Port 5000)
- Listens for incoming TCP connections
- Handles **multiple concurrent clients** (configurable limit)
- Processes **continuous binary streams** with proper message framing
- Implements **connection pooling** and **timeout handling**

### 2. **Binary Protocol Processing**
```
Message Format (Big Endian):
[Sync Word: 0xAA55][Device ID: 4 bytes][Counter: 2 bytes][Type: 1 byte][Length: 2 bytes][Payload: variable]
```

**Validation:**
- ✅ Sync word verification (0xAA55)
- ✅ Payload length validation
- ✅ Message completeness check
- ✅ Big Endian format handling

### 3. **Message Deduplication**
- **Per-device counter tracking** (Device ID + Message Counter)
- **Memory-efficient cache** with automatic cleanup
- **Thread-safe operations** using concurrent collections

### 4. **Type-Based Message Routing**

| Message Types | Destination | Format |
|---------------|-------------|---------|
| **2, 11, 13** | `device-messages` topic | **Standardized JSON** |
| **1, 3, 12, 14** | `device-events` topic | **Raw payload bytes only** |
| **Others** | Ignored | Logged as unknown |

## 🎯 Kafka Integration

### **Where Messages Are Saved:**

#### Device Messages Topic (`device-messages`)
```json
{
  "deviceId": "01-02-03-04",
  "messageCounter": 1,
  "messageType": 2,
  "timestamp": "2024-01-01T12:00:00Z",
  "payload": "AQIDBA==",  // Base64 encoded
  "payloadSize": 4,
  "correlationId": "trace-id-123"
}
```

#### Device Events Topic (`device-events`)
```
AQIDBA==  // Raw payload bytes (base64 encoded) - NO metadata wrapper
```
**Direct routing:** Only the payload bytes are sent, no JSON structure, no metadata.

### **Kafka Configuration:**
- **Bootstrap Servers:** `localhost:9092` (configurable)
- **Producer Settings:** Idempotent, compressed (Snappy), acks=all
- **Error Handling:** Automatic retries with proper logging
- **Headers:** Source tracking and versioning

## 🚀 How to Test the Code

### **Option 1: Using the Built-in MessageSender**

1. **Start the TCP Listener Service:**
```bash
cd MessageSender
dotnet run --urls http://localhost:8080
```

2. **Run the MessageSender Test Client:**
```bash
# In a new terminal window
cd MessageSender  
dotnet run MessageSender.cs
```

The MessageSender will automatically:
- Send Device Messages (types 2, 11, 13) → `device-messages` topic
- Send Device Events (types 1, 3, 12, 14) → `device-events` topic  
- Test duplicate message rejection
- Test unknown message type handling

### **Option 2: Manual Testing with Custom Messages**

```csharp
// Create custom messages
var uri = new Uri("tcp://localhost:5000");
MessageSender.SendMessage(uri, 
    deviceId: new byte[] { 0x01, 0x02, 0x03, 0x04 },
    messageCounter: 1,
    messageType: 2,  // Device Message
    payload: new byte[] { 0x10, 0x20, 0x30 }
);
```

### **Option 3: Using Unit Tests**
```bash
dotnet test
```

## 📊 Monitoring & Observability

### **Health Check Endpoint**
```
GET http://localhost:8080/health
```
Returns detailed health status in JSON format.

### **Metrics Endpoint** 
```
GET http://localhost:8080/metrics
```
Prometheus-format metrics including:
- Message processing rates
- Connection counts  
- Error rates
- Processing durations

### **OpenTelemetry Tracing**
- **Activity tracing** through the entire message pipeline
- **Correlation IDs** for distributed tracing
- **Performance metrics** for bottleneck identification

## 🔧 Configuration

### **appsettings.json**
```json
{
  "TcpListener": {
    "Port": 5000,
    "MaxConcurrentClients": 100,
    "BufferSize": 4096,
    "ClientTimeoutMs": 30000
  },
  "Kafka": {
    "BootstrapServers": "localhost:9092",
    "DeviceMessageTopic": "device-messages",
    "DeviceEventTopic": "device-events"
  }
}
```

## 🏛️ Architecture Principles Applied

### **SOLID Principles:**
- **SRP:** Each class has single responsibility (TCP handling, Kafka publishing, metrics)
- **OCP:** Extensible through interfaces (`IMessageHandler`, `IBinaryListener`)
- **LSP:** Proper inheritance contracts
- **ISP:** Focused interfaces
- **DIP:** Dependency injection throughout

### **Production-Ready Features:**
- ✅ **Comprehensive error handling** with structured logging
- ✅ **Resource management** with proper disposal patterns
- ✅ **Scalability** with concurrent connection handling
- ✅ **Monitoring** with health checks and metrics
- ✅ **Resilience** with timeouts and graceful degradation
- ✅ **Configuration-driven** design for different environments

## 🎯 Interview Demonstration Points

1. **Scalable TCP Processing** - Handles multiple concurrent connections efficiently
2. **Message Protocol Implementation** - Correct Big Endian parsing and validation
3. **Type-Based Routing** - Smart message classification and processing
4. **Production Monitoring** - Health checks, metrics, and distributed tracing
5. **Enterprise Architecture** - SOLID principles, dependency injection, clean separation of concerns
6. **Error Resilience** - Graceful handling of network issues, invalid messages, and Kafka outages