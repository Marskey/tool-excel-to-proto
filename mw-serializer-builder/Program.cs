using System;
using System.Collections.Generic;
using System.Text;

namespace mw_serializer_builder
{
    class Program
    {
        static void Main(string[] args)
        {
            string path = args[0];

            Console.WriteLine(path);

            var ass = System.Reflection.Assembly.LoadFile(path);

            var types = ass.GetTypes();

            var tm = ProtoBuf.Meta.TypeModel.Create();

            int split = int.Parse(args[2]);
            int c = 0;
            int i = 0;
            foreach(var t in types)
            {
                if(t.Name.EndsWith("_ValidateInfo"))
                {
                    continue;
                }

                tm.Add(t, true);
                i++;
                if (i > split)
                {
                    tm.Compile(args[1].Replace("-", "_") + c.ToString(), args[1] + c.ToString() + ".dll");
                    c++;
                    tm = ProtoBuf.Meta.TypeModel.Create();
                    i = 0;
                }
            }

            if(i > 0)
                tm.Compile(args[1].Replace("-", "_") + c.ToString(), args[1] + c.ToString() + ".dll");
        }
    }
}
