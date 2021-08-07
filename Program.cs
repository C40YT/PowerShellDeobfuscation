using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PowershellDeobfuscation
{
    class Program
    {
        static void Main(string[] args)
        {
            Deobfuscation();
        }

        static void Deobfuscation()
        {
            string sampleScript = "";

            string scriptPath = "Data\\sample_1.ps1";
            string modelPath = "Data\\ObfuscationClassifierModel.zip";

            FileInfo file = new FileInfo(scriptPath);

            try
            {
                FileStream fs = new FileStream(scriptPath, FileMode.Open, FileAccess.Read);
                TextReader scriptIn = new StreamReader(fs);
                sampleScript = scriptIn.ReadToEnd();
            }
            catch
            {
                return;
            }

            AstTree tree = new AstTree(sampleScript);

            tree.InitPipeSubTree();

            // model是用于判别有无混淆的模型，如果有混淆则会尝试解混淆处理
            TraverseSubTree traverser = new TraverseSubTree(modelPath);
            traverser.TraverseCheckPipeSubtree(tree);
            // 最终tree中所有的PipeNode节点都会被处理分析，然后向上更新得到updatedCommand
            Console.Out.WriteLine(tree.root.updatedCommand);
        }
    }
}
