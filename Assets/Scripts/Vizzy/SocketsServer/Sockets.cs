using Assets.Scripts.Vizzy.SocketsService;
using ModApi.Craft.Program;
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


            if (!int.TryParse(portString, out int port))
            {
                Debug.LogError("Invalid port number: " + portString);
                return new ProgramInstruction();
            }

            SocketsServiceManager.CreateServer(context.Craft, port);
            //context.Log.Log("textValue1", null, null);
            ExpressionResult expressionResult = new ExpressionResult();
            return base.Execute(context);
        }

    }

    [Serializable]
    public class SentSocketsInstruction : ProgramInstruction
    {

        public override ProgramInstruction Execute(IThreadContext context)
        {
            string portString = this.GetExpression(1).Evaluate(context).TextValue;
            IReadOnlyList<ExpressionListItem> data = base.GetExpression(0).Evaluate(context).ListValue;

            if (!int.TryParse(portString, out int port))
            {
                Debug.LogError("Invalid port number: " + portString);
                return new ProgramInstruction();
            }
            string data1 = "";
            foreach (var item in data)
            {
                data1 += item.StringValue + "<<";
            }
            //Debug.Log(data1);
            byte[] dataBytes = System.Text.Encoding.UTF8.GetBytes(data1);
            SocketsServiceManager.Send(port, dataBytes);

            return base.Execute(context);
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
}

