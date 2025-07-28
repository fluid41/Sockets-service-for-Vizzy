using Assets.Scripts.Vizzy.SocketsService;
using ModApi.Craft.Program;
using ModApi.Craft.Program.Craft;
using ModApi.Craft.Program.Instructions;
using System;
using System.Collections.Generic;
using UnityEngine;

namespace Assets.Scripts.Vizzy.Sockets
{
    [Serializable]
    public class StartSocketsInstruction : ProgramInstruction
    {

        public override ProgramInstruction Execute(IThreadContext context)
        {
            string portString = this.GetExpression(0).Evaluate(context).TextValue;
            string bufferString = this.GetExpression(1).Evaluate(context).TextValue;
            bool useVzVariableBuffer = this.GetExpression(2).Evaluate(context).BoolValue;
            string ErrorMessage = null;
            if (!int.TryParse(portString, out int port))
            {
                //Debug.LogError("Invalid port number: " + portString);
                context.Log.LogError("Invalid port number: " + portString, null, null);
                ErrorMessage = "StartSockets : port error";
            }
            if (!int.TryParse(bufferString, out int buffer))
            {
                //Debug.LogError("Invalid buffer number: " + bufferString);
                context.Log.LogError("Invalid buffer number: " + bufferString, null, null);
                ErrorMessage = "StartSockets : buffer error";
            }
            if (ErrorMessage == null)
            {
                if (!SocketsServiceManager.CreateServer(context, port, buffer, useVzVariableBuffer))
                {
                    //Debug.LogError("Failed to create server on port: " + portString);
                    context.Log.LogError("Failed to create server on port: " + portString, null, null);
                    ErrorMessage = "StartSockets : create error";
                }
            }

            if (ErrorMessage != null)
            {
                List<ExpressionListItem> list = new List<ExpressionListItem>();

                list.Add(ErrorMessage);
                list.Add(portString);
                list.Add(bufferString);

                context.Craft.BroadcastMessage(BroadcastScope.Program, "socket error", new ExpressionResult(list));
            }

            return base.Execute(context);
        }

    }




    [Serializable]
    public class SentSocketsInstruction : ProgramInstruction
    {

        public override ProgramInstruction Execute(IThreadContext context)
        {
            IReadOnlyList<ExpressionListItem> data = base.GetExpression(0).Evaluate(context).ListValue;
            string portString = this.GetExpression(1).Evaluate(context).TextValue;
            string ErrorMessage = null;

            if (!int.TryParse(portString, out int port))
            {
                //Debug.LogError("Invalid port number: " + portString);
                context.Log.LogError("Invalid port number: " + portString, null, null);
                ErrorMessage = "SentSockets : port error";
            }
            else
            {
                string data1 = "";
                foreach (var item in data)
                {
                    data1 += item.StringValue + "<<";
                }
                //Debug.Log(data1);
                byte[] dataBytes = System.Text.Encoding.UTF8.GetBytes(data1);
                if (!SocketsServiceManager.Send(port, dataBytes))
                {
                    //Debug.LogError("Failed to send data to port: " + portString);
                    ErrorMessage = "SentSockets : send error";
                    context.Log.LogError("Failed to send data to port: " + portString, null, null);
                }
            }

            if (ErrorMessage != null)
            {
                List<ExpressionListItem> list = new List<ExpressionListItem>();
                list.Add(ErrorMessage);
                list.Add(portString);
                foreach (var item in data)
                {
                    list.Add(item);
                }
                context.Craft.BroadcastMessage(BroadcastScope.Program, "socket error", new ExpressionResult(list));
            }
            return base.Execute(context);
        }

    }








    [Serializable]
    public class StopSocketsInstruction : ProgramInstruction
    {

        public override ProgramInstruction Execute(IThreadContext context)
        {
            string portString = this.GetExpression(0).Evaluate(context).TextValue;
            string ErrorMessage = null;
            if (!int.TryParse(portString, out int port))
            {
                //Debug.LogError("Invalid port number: " + portString);
                context.Log.LogError("Invalid port number: " + portString, null, null);
                ErrorMessage = "StopSockets : port error";
            }
            if (!SocketsServiceManager.CloseServer(port))
            {
                //Debug.LogError("Failed to close server on port: " + portString);
                context.Log.LogError("Failed to close server on port: " + portString, null, null);
                ErrorMessage = "StopSockets : close error";
            }

            if (ErrorMessage != null)
            {
                List<ExpressionListItem> list = new List<ExpressionListItem>();
                list.Add(ErrorMessage);
                list.Add(portString);
                context.Craft.BroadcastMessage(BroadcastScope.Program, "socket error", new ExpressionResult(list));
                context.Log.LogError("Failed to close server on port: " + portString, null, null);
            }
            return base.Execute(context);
        }

    }




    [Serializable]
    public class UpdateVzVarBufferInstruction : ProgramInstruction
    {
        public override ProgramInstruction Execute(IThreadContext context)
        {
            string portString = this.GetExpression(0).Evaluate(context).TextValue;
            string ErrorMessage = null;
            // context.GetOrCreateGlobalVariable("portString").Value.Set(this.GetExpression(0).Evaluate(context));

            if (!int.TryParse(portString, out int port))
            {
                context.Log.LogError("Invalid port number: " + portString, null, null);
                ErrorMessage = "UpdateVzVarBuffer : port error";
            }
            else
            {
                // 从队列中取出一个数据项（先入先出，读取即为取出）
                ExpressionResult nextItem = SocketsServiceManager.GetNextVzVariableBufferItem(port);

                string variableName = $"Port{port}SocketReceivedData";

                if (nextItem != null)
                {
                    // 如果队列中有数据，设置该数据到以端口命名的变量
                    context.GetOrCreateGlobalVariable(variableName).Value.Set(nextItem);
                }
                else
                {
                    // 如果队列中没有数据，设置为空列表
                    var emptyList = new List<ExpressionListItem>();
                    context.GetOrCreateGlobalVariable(variableName).Value.Set(new ExpressionResult(emptyList));
                }
            }

            if (ErrorMessage != null)
            {
                List<ExpressionListItem> list = new List<ExpressionListItem>();
                list.Add(ErrorMessage);
                list.Add(portString);
                context.Craft.BroadcastMessage(BroadcastScope.Program, "socket error", new ExpressionResult(list));
            }

            return base.Execute(context);
        }

    }

    [Serializable]
    public class PreciseWaitInstruction : ProgramInstruction
    {
        public override ProgramInstruction Execute(IThreadContext context)
        {
            double waitTime = this.GetExpression(0).Evaluate(context).NumberValue;
            
            // 实现精确等待功能
            if (waitTime > 0)
            {
                // 将等待时间转换为毫秒并执行等待
                System.Threading.Thread.Sleep((int)(waitTime * 1000));
            }
            
            return base.Execute(context);
        }

    }

    //[Serializable]
    //public class RecvInstruction : ProgramInstruction
    //{
    //    public ProgramEventType EventType
    //    {
    //        get
    //        {
    //            return this._event;
    //        }
    //    }

    //    public override ProgramInstruction Execute(IThreadContext context)
    //    {
    //        return base.Execute(context);
    //    }

    //    [ProgramNodeProperty]
    //    private ProgramEventType _event = ProgramEventType.FlightStart;
    //}
}