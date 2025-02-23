using Assets.Scripts.Vizzy.SocketsService;
using ModApi.Craft.Program;
using ModApi.Craft.Program.Instructions;
using System;
using System.Collections.Generic;
using UnityEngine;

namespace Assets.Scripts.Vizzy.Sockets
{
    [Serializable]
    public class StartSocketsExpression : ProgramExpression
    {

        public override ExpressionResult Evaluate(IThreadContext context)
        {
            string portString = this.GetExpression(0).Evaluate(context).TextValue;
            string bufferString = this.GetExpression(1).Evaluate(context).TextValue;
            ExpressionResult expressionResult = new ExpressionResult();
            expressionResult.BoolValue = false;
            if (!int.TryParse(portString, out int port))
            {
                Debug.LogError("Invalid port number: " + portString);
                return expressionResult;
            }
            if (!int.TryParse(bufferString, out int buffer))
            {
                Debug.LogError("Invalid buffer number: " + portString);
                return expressionResult;
            }

            SocketsServiceManager.CreateServer(context.Craft, port, buffer);
            //context.Log.Log("textValue1", null, null);
            expressionResult.BoolValue = true;
            return expressionResult;
        }


        public override bool IsBoolean
        {
            get
            {
                return true;
            }
        }
    }




    [Serializable]
    public class SentSocketsExpression : ProgramExpression
    {

        public override ExpressionResult Evaluate(IThreadContext context)
        {
            string portString = this.GetExpression(1).Evaluate(context).TextValue;
            IReadOnlyList<ExpressionListItem> data = base.GetExpression(0).Evaluate(context).ListValue;
            ExpressionResult expressionResult = new ExpressionResult();
            expressionResult.BoolValue = false;
            if (!int.TryParse(portString, out int port))
            {
                Debug.LogError("Invalid port number: " + portString);
                return expressionResult;
            }
            string data1 = "";
            foreach (var item in data)
            {
                data1 += item.StringValue + "<<";
            }
            //Debug.Log(data1);
            byte[] dataBytes = System.Text.Encoding.UTF8.GetBytes(data1);
            expressionResult.BoolValue = SocketsServiceManager.Send(port, dataBytes);
            return expressionResult;
        }


        public override bool IsBoolean
        {
            get
            {
                return true;
            }
        }
    }








    [Serializable]
    public class StopSocketsInstruction : ProgramInstruction
    {

        public override ProgramInstruction Execute(IThreadContext context)
        {
            string portString = this.GetExpression(0).Evaluate(context).TextValue;
            if (!int.TryParse(portString, out int port))
            {
                Debug.LogError("Invalid port number: " + portString);
                return new ProgramInstruction();
            }

            SocketsServiceManager.CloseServer(port);

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