using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using System.Management.Automation;
using System.Management.Automation.Language;
using System.Collections.ObjectModel;
using System.Management.Automation.Runspaces;

namespace PowershellDeobfuscation
{
    public class InstancePF
    {
        PowerShell psInstance = PowerShell.Create();

        public InstancePF()
        {

        }

        // 核心去混淆的代码，通过 PowerShell.Create()创建实例，然后对这部分Ast类型为PipeAst的脚本执行之后得到去混淆的结果
        // 返回执行得到的脚本字符串，一次作为新的脚本
        public string addScript(string script)
        {
            psInstance.AddScript(script);
            Collection<PSObject> psOutput;
            psOutput = psInstance.Invoke();


            StringBuilder output = new StringBuilder();
            foreach (var ob in psOutput)
            {
                if (!ob.TypeNames.Contains("System.String"))
                {
                    continue;
                }

                if (ob.TypeNames.Count() == 2)
                    output.Append("\"" + ob.BaseObject.ToString() + "\"");
                else
                    output.Append(ob.BaseObject.ToString());
            }

            return output.ToString();
        }

        // deprecated，直接运行脚本
        public static void RunScript(string scripts)
        {
            try
            {
                Runspace runspace = RunspaceFactory.CreateRunspace();
                runspace.Open();
                Pipeline pipeline = runspace.CreatePipeline();
                pipeline.Commands.AddScript(scripts);
                var results = pipeline.Invoke();
                runspace.Close();

            }
            catch (Exception e)
            {
                Console.WriteLine(DateTime.Now.ToString() + "error：" + e.Message);
            }
        }
    }
}
