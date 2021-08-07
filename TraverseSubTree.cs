using System;
using System.Collections.Generic;
using System.IO;
using System.Text;


namespace PowershellDeobfuscation
{
    public class TraverseSubTree
    {
        string modelPath = "Data\\ObfuscationClassifierModel.zip";

        Classifier c = new Classifier();
        InstancePF psIns = new InstancePF();

        public TraverseSubTree()
        {
            c.initPredEngine(modelPath);
        }

        public TraverseSubTree(string modelPath)
        {
            this.modelPath = modelPath;
            c.initPredEngine(modelPath);
        }

        public void ReverseTraverseCheckSubtree(AstTree tree)
        {
            AstNode node = tree.root;

            Queue<AstNode> unvisitedNodeQueue = new Queue<AstNode>();
            unvisitedNodeQueue.Enqueue(node);

            Stack<AstNode> pipeNodeStack = new Stack<AstNode>();

            while (unvisitedNodeQueue.Count > 0)
            {
                AstNode n = unvisitedNodeQueue.Dequeue();
                
                if (n.ast.GetType().ToString() == "System.Management.Automation.Language.PipelineAst")
                {
                    pipeNodeStack.Push(n);
                }

                foreach (AstNode nc in n.childList)
                {
                    unvisitedNodeQueue.Enqueue(nc);
                }
            }
            
            while (pipeNodeStack.Count > 0)
            {
                AstNode n = pipeNodeStack.Pop();
                Classifier.ClassifierResult result = c.testWithModel(AstTree.Tree2Feature(n));

                if (result != Classifier.ClassifierResult.unobfuscated)
                {
                    // what to do with the obfuscated sub-tree
                    string returnScript = psIns.addScript(n.ast.Extent.Text);
                    Console.Out.WriteLine(String.Format("Script:{0}, result:{1}, Deobfuscation:{2}", n.ast.Extent.Text, result, returnScript));

                    tree.AddSubTree(n, returnScript);
                }
                else
                {
                    Console.Out.WriteLine(String.Format("Script:{0}, result:{1}", n.ast.Extent.Text, result));
                }
            }
        }

        public class DeobfuscationResult
        {
            public int obfuscated = 0;
            public string originalScript = "";
            public string deobfuscatedScript = "";
            public string shapedScript = "";
        }

        public List<DeobfuscationResult> ReverseTraverseCheckSubtreeWithExperimentalOutput(AstTree tree)
        {
            AstNode node = tree.root;

            Queue<AstNode> unvisitedNodeQueue = new Queue<AstNode>();
            unvisitedNodeQueue.Enqueue(node);

            Stack<AstNode> pipeNodeStack = new Stack<AstNode>();

            while (unvisitedNodeQueue.Count > 0)
            {
                AstNode n = unvisitedNodeQueue.Dequeue();
                
                if (n.ast.GetType().ToString() == "System.Management.Automation.Language.PipelineAst")
                {
                    pipeNodeStack.Push(n);
                }

                foreach (AstNode nc in n.childList)
                {
                    unvisitedNodeQueue.Enqueue(nc);
                }
            }

            InstancePF psIns = new InstancePF();

            List<DeobfuscationResult> outputList = new List<DeobfuscationResult>();
            while (pipeNodeStack.Count > 0)
            {
                AstNode n = pipeNodeStack.Pop();
                Classifier.ClassifierResult result = c.testWithModel(AstTree.Tree2Feature(n));

                DeobfuscationResult output = new DeobfuscationResult();
                output.originalScript = AstNode.GetShapedScript(n.ast.Extent.Text);


                if (result != Classifier.ClassifierResult.unobfuscated)
                {
                    string returnScript = psIns.addScript(n.ast.Extent.Text);
                    Console.Out.WriteLine(String.Format("Script:{0}, result:{1}, Deobfuscation:{2}", n.ast.Extent.Text, result, returnScript));

                    output.obfuscated = 1;
                    output.deobfuscatedScript = AstNode.GetShapedScript(returnScript);
                    
                    if (returnScript.Length != 0)
                        tree.RemoveSubTree(n, n.childList[0]);
                    tree.AddSubTree(n, returnScript);
                }
                else
                {
                    Console.Out.WriteLine(String.Format("Script:{0}, result:{1}", n.ast.Extent.Text, result));
                }
                outputList.Add(output);
            }
            return outputList;
        }

        public void TraverseCheckSubtree(AstTree tree)
        {
            TraverseCheckSubtree(tree.root);
        }

        public void TraverseCheckSubtree(AstNode node)
        {
            Queue<AstNode> unvisitedNodeQueue = new Queue<AstNode>();
            unvisitedNodeQueue.Enqueue(node);

            while (unvisitedNodeQueue.Count > 0)
            {
                AstNode n = unvisitedNodeQueue.Dequeue();
                
                if (n.ast.GetType().ToString() == "System.Management.Automation.Language.PipelineAst")
                {
                    AstData data = AstTree.Tree2Feature(n);
                    Classifier.ClassifierResult result = c.testWithModel(data);
                    Console.Out.WriteLine(String.Format("Script:{0}, result:{1}", n.ast.Extent.Text, result.ToString()));
                }

                foreach (AstNode nc in n.childList)
                {
                    unvisitedNodeQueue.Enqueue(nc);
                }
            }
        }

        // 对Tree中的所有PipeNode节点进行自底向上的解析，最终将所有的PipeNode节点解析完成变为CleanPipeNode
        // 返回这部分脚本在模型的判断是是否存在混淆
        public int TraverseCheckPipeSubtree(AstTree tree)
        {
            int obfuscatedOrNot = 0;

            Queue<AstNode> pipeQueue = new Queue<AstNode>();
            AstTree subtree;
            AstNode parent;

            foreach (AstNode node in tree.nodeList)
            {
                if (node.type == AstNode.NodeType.pipeNode)
                {
                    pipeQueue.Enqueue(node);
                }
            }

            AstNode top;
            // DFS循环遍历所有的PipeNode节点并进行处理
            while (pipeQueue.Count != 0)
            {
                top = pipeQueue.Dequeue();
                int subtreePipeCount = -1;

                if (top.pipeNodeCount != 0)
                {
                    pipeQueue.Enqueue(top);
                }
                else
                {
                    // 通过模型判断如果这部分AST是否还存在混淆，特征包括了 各个ast种类的数量，脚本的熵，脚本长度，最大token长度和平均token长度的信息
                    // 特征有点少？
                    if (c.testWithModel(AstTree.Tree2Feature(top)) == Classifier.ClassifierResult.unobfuscated)
                    {
                        // 将这部分AstNode标记为干净的无混淆的AstNode
                        top.type = AstNode.NodeType.cleanPipeNode;
                        string feature = new Script2Vector(top.command).ToAStString();
                        Console.Out.WriteLine(feature);
                        if (top.updatedCommand.Length == 0)
                            top.updatedCommand = top.command;
                    }
                    else
                    {
                        // 还存在混淆的话，对脚本进行去混淆处理后重新建立 AstTree
                        try
                        {
                            string tempCommand = "";
                            tempCommand = psIns.addScript(top.command);
                            if (tempCommand.Length == 0)
                            {
                                top.type = AstNode.NodeType.cleanPipeNode;
                                goto a;
                            }
                            else
                            {
                                // todo 代码好像没写完？Parent的Type是Command怎么了？会去执行？
                                tempCommand = CheckParents(tempCommand, top);
                                top.updatedCommand = tempCommand;
                            }
                        }
                        catch
                        {
                            top.type = AstNode.NodeType.cleanPipeNode;
                            goto a;
                        }
                        obfuscatedOrNot = 1;

                        // 对于混淆节点处理并更新了原节点后，将新增的PipeNode节点添加到AstTree中用于后续的遍历处理。
                        subtree = new AstTree(top.updatedCommand, AstNode.NodeType.replaceNode);
                        subtree.InitPipeSubTree();

                        tree.ReplaceSubTree(top.parent, top, subtree);
                        
                        Queue<AstNode> newQueue = AstTree.FindAllPipeNodeInSubTree(subtree.root);
                        AstNode node;
                        while (newQueue.Count != 0)
                        {
                            // 这又是在干嘛... 为什么直接Dequeue了，不应该是加入检测队列然后继续检测与执行吗？
                            node = newQueue.Dequeue();
                        }

                    a:;
                    }
                    
                    parent = top.parent;
                    while (!(parent.parent == null || parent.type == AstNode.NodeType.pipeNode))
                    {
                        parent = parent.parent;
                    }
                    // AstNode中包含了command和updated command，这里是将为PipeNode类型的祖先中
                    // 与top.command相关的部分都修改为updatedCommand来更新PipeNode节点
                    parent.UpdateCommand(top.command, top.updatedCommand);
                    experimentOut.WriteLine(String.Format("{0}`{2}`{1}", AstNode.GetShapedScript(top.command), AstNode.GetShapedScript(top.updatedCommand), top.command != top.updatedCommand));

                    // 处理完该PipeNode之后，所有祖先节点的PipeNode Count都需要减1，
                    // 如果祖先的PipeNode Count为0则表示该祖先节点的审查已经完成，即解混淆处理已经完成
                    parent = top.parent;
                    while (parent.parent != null)
                    {
                        parent.pipeNodeCount += subtreePipeCount;

                        if (parent.pipeNodeCount == 0)
                        {
                            tree.Shrink(parent);
                        }

                        parent = parent.parent;
                    }
                }
            }
            return obfuscatedOrNot;
        }

        private string CheckParents(string tempCommand, AstNode top)
        {
            while (top.parent.GetASTType().Contains("Command"))
            {
                // ？？？这部分代码呢，如果包含了Command就去执行吗？
            }
            return tempCommand;
        }

        public static FileStream fs = new FileStream("experimentResult.ps1", FileMode.OpenOrCreate, FileAccess.Write);
        public static TextWriter experimentOut = new StreamWriter(fs);
    }
}
