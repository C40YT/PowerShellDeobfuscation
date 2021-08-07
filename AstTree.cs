using System;
using System.Management.Automation;
using System.Management.Automation.Language;
using System.Text;
using System.Text.RegularExpressions;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.IO;


namespace PowershellDeobfuscation
{

    // 表示一个AST树的节点
    public class AstNode
    {
        public Ast ast;
        public enum NodeType { originNode, replaceNode, pipeNode, cleanPipeNode }; // 定义了类别主要是对pipeNode进行处理
        public NodeType type;

        public AstNode parent = null;
        public List<AstNode> childList = new List<AstNode>();

        // For PipeSubTree，将返回更新处理完的节点command值
        // 该节点构成的树中，除了该节点外所有孩子节点的PipeNode总数
        // 如果该值为0，则可以将该节点包含的所有子节点忽略，只留下当前节点
        public int pipeNodeCount = 0; 
        public string command = ""; //  原本的command
        public string updatedCommand = "";  // 更新完后的command

        public class ReplaceSet
        {
            public string oldString;
            public string newString;

            public ReplaceSet(string oldString, string newString)
            {
                this.oldString = oldString;
                this.newString = newString;
            }
        }
        public List<ReplaceSet> replaceList = new List<ReplaceSet>();
        // ===============

        // For Code Similarity
        public string astString = "";
        public double matched = 0;
        // ===============

        // 通过AST节点构造自定义树的节点
        public AstNode(Ast ast)
        {
            this.ast = ast;
            if (ast.GetType().ToString() == "System.Management.Automation.Language.PipelineAst")
            {
                type = NodeType.pipeNode;
            }
            else
            {
                // 对于所有非PipelineAst节点来说均定义为originNode
                type = NodeType.originNode;
            }
        }

        public AstNode(Ast ast, NodeType type)
        {
            this.ast = ast;
            if (ast.GetType().ToString() == "System.Management.Automation.Language.PipelineAst")
            {
                this.type = NodeType.pipeNode;
            }
            else
            {
                this.type = type;
            }
        }

        public bool HasChild()
        {
            return childList != null && childList.Count() != 0;
        }

        override public string ToString()
        {
            return ast.Extent.Text;
        }

        public string GetASTType()
        {
            return ast.GetType().ToString().Split('.')[4];
        }

        public string GetScript()
        {
            return ast.Extent.Text.Replace("\n", " ").Replace("\t", "").Replace("\r", "");
        }

        public static string GetShapedScript(string script)
        {
            if (script.Length > 60)
            {
                return (script.Substring(0, 30) + "..." + script.Substring(script.Length - 30)).Replace("\n", " ").Replace("\t", "").Replace("\r", "");
            }
            else
                return script.Replace("\n", " ").Replace("\t", "").Replace("\r", "");
        }

        public string GetShapedScript()
        {
            return GetShapedScript(ast.Extent.Text);
        }

        public static string GetColorFromType(NodeType type)
        {
            string color = "black";

            switch (type)
            {
                case AstNode.NodeType.pipeNode: color = "red"; break;
                case AstNode.NodeType.cleanPipeNode: color = "coral"; break;
                case AstNode.NodeType.replaceNode: color = "blue"; break;
                case AstNode.NodeType.originNode: color = "black"; break;
                default: break;
            }

            return color;
        }

        public void UpdateCommand(string oldCommands, string newCommands)
        {
            if (newCommands.Length == 0) // why?
                return;

            foreach (var set in replaceList)
            {
                oldCommands = oldCommands.Replace(set.oldString, set.newString);
            }

            if (oldCommands != newCommands)
            {
                replaceList.Add(new ReplaceSet(oldCommands, newCommands));
            }
            if (updatedCommand.Length == 0)
                updatedCommand = command.Replace(oldCommands, newCommands);
            else
                updatedCommand = updatedCommand.Replace(oldCommands, newCommands);
        }
    }

    // 表示一个AST树，用来存储AST
    public class AstTree
    {
        public AstNode root;    //树结构的root
        public List<AstNode> nodeList = new List<AstNode>(); //存储所有AST节点的链表
        public enum TreeType { basic, pipeSubTree };
        public TreeType type = TreeType.basic;

        // 对脚本进行解析，得到数结构存储的 AST语法树木
        public AstTree(string script)
        {
            // 首先对符号 ` 进行处理，做清理操作
            script = Preprocess(script);
            // 提取AST语法树
            ScriptBlockAst sb = System.Management.Automation.Language.Parser.ParseInput(script, out Token[] tokens, out ParseError[] errors);
            IEnumerable<Ast> astnodes = sb.FindAll(delegate (Ast t) { return true; }, true);
            List<Ast> astnodeList = astnodes.ToList<Ast>();
            ConstructTree(astnodeList);
        }

        public AstTree(string script, AstNode.NodeType type) : this(script)
        {
            foreach (var node in nodeList)
            {
                if (node.type != AstNode.NodeType.pipeNode)
                    node.type = type;
            }
        }

        public AstTree(AstNode node)
        {
            root = node;
            Traverse(node);
        }

        static string Preprocess(string sampleScript)
        {
            string result = sampleScript;
            result = result.Replace("`", "");//.ToLower();

            var temp = Regex.Matches(result, @"{([^}]*)}");

            return result;
        }

        public void Traverse(AstNode node)
        {
            Queue<AstNode> unvisitedNodeQueue = new Queue<AstNode>();
            unvisitedNodeQueue.Enqueue(node);

            while (unvisitedNodeQueue.Count > 0)
            {
                AstNode n = unvisitedNodeQueue.Dequeue();

                // traverse action...
                nodeList.Add(n);

                foreach (AstNode nc in n.childList)
                {
                    unvisitedNodeQueue.Enqueue(nc);
                }
            }
        }

        public void AddSubTree(AstNode node, string script)
        {
            if (script == "")
                return;

            AddSubTree(node, new AstTree(script));
        }

        public void AddSubTree(AstNode node, string script, int indexOfNode)
        {
            if (script == "")
                return;

            AddSubTree(node, new AstTree(script), indexOfNode);
        }

        void AddSubTree(AstNode node, AstTree tree)
        {
            AddSubTree(node, tree, 0);
        }

        public void AddSubTree(AstNode node, AstTree tree, int indexOfNode)
        {
            tree.root.parent = node;

            foreach (var n in tree.nodeList)
            {
                //n.type = AstNode.NodeType.replaceNode;

                nodeList.Add(n);
            }

            try
            {
                node.childList.Insert(indexOfNode, tree.root);
            }
            catch
            {
                return;
            }
        }

        // 将一个节点以及其所有的子节点都从AstTree中删除
        public int RemoveSubTree(AstNode parent, AstNode node)
        {
            int indexOfNode = parent.childList.IndexOf(node);
            parent.childList.Remove(node);

            Queue<AstNode> unvisitedNodeQueue = new Queue<AstNode>();
            unvisitedNodeQueue.Enqueue(node);

            while (unvisitedNodeQueue.Count > 0)
            {
                AstNode n = unvisitedNodeQueue.Dequeue();

                nodeList.Remove(n);

                foreach (AstNode nc in n.childList)
                {
                    unvisitedNodeQueue.Enqueue(nc);
                }
            }

            return indexOfNode;
        }

        // 用新的AstTree替换当中的某个AstNode节点对应的AstTree
        public void ReplaceSubTree(AstNode parent, AstNode originalNode, AstTree replacedTree)
        {
            int index = RemoveSubTree(parent, originalNode);
            AddSubTree(parent, replacedTree, index);
        }

        public void ReplaceSubTree(AstNode parent, AstNode originalNode, string replacedScript)
        {
            int index = RemoveSubTree(parent, originalNode);
            AddSubTree(parent, replacedScript, index);
        }

        // 将Ast语法树用树的结构进行存储，通过hash判断父子关系
        public void ConstructTree(List<Ast> astnodeList)
        {
            root = new AstNode(astnodeList[0]);
            nodeList.Add(root);

            int nodeCount = astnodeList.Count();
            for (int i = 1; i < nodeCount; i++)
            {
                nodeList.Add(new AstNode(astnodeList[i]));
            }

            for (int i = 0; i < nodeCount; i++)
            {
                var iHash = nodeList[i].ast.GetHashCode();
                //System.Console.Out.WriteLine(iHash);
                for (int j = 0; j < nodeCount; j++)
                {
                    if (nodeList[j].ast.Parent == null)
                    {
                        continue;
                    }
                    if (iHash == nodeList[j].ast.Parent.GetHashCode())
                    {
                        nodeList[i].childList.Add(nodeList[j]);
                        nodeList[j].parent = nodeList[i];
                    }
                }
            }
        }

        public string GetTreeTag(AstNode node)
        {
            if (type == TreeType.basic)
                return node.ast.GetType().ToString().Split('.')[4] + "\\n" + node.GetShapedScript().Replace("\n", " ").Replace("\"", "\\\"").Replace("\t", "");
            else if (type == TreeType.pipeSubTree)
                return node.ast.GetType().ToString().Split('.')[4]
                    + "\\n"
                    + AstNode.GetShapedScript(node.command).Replace("\n", " ").Replace("\"", "\\\"").Replace("\t", "")
                    + "\\n"
                    + AstNode.GetShapedScript(node.updatedCommand).Replace("\n", " ").Replace("\"", "\\\"").Replace("\t", "")
                    + "\\n"
                    + node.pipeNodeCount
                    ;
            else
                return "";
        }

        public void DrawTreewithDot(string dotFileName)
        {
            FileStream fs = new FileStream(dotFileName, FileMode.OpenOrCreate, FileAccess.ReadWrite);
            TextWriter dot = new StreamWriter(fs);

            dot.WriteLine("digraph G {");

            string color = "black";
            foreach (AstNode node in nodeList)
            {
                color = AstNode.GetColorFromType(node.type);

                Console.Out.WriteLine(GetTreeTag(node));
                dot.WriteLine(String.Format("\tn{0} [label=\"{1}\", color=\"{2}\"]", node.GetHashCode(), GetTreeTag(node), color));
            }

            foreach (AstNode node in nodeList)
            {
                if (node.childList.Count() == 0)
                    continue;
                else
                {
                    foreach (AstNode child in node.childList)
                        dot.WriteLine(String.Format("\tn{0} -> n{1}", node.GetHashCode(), child.GetHashCode()));
                }
            }

            dot.WriteLine("}");
            dot.Close();
            fs.Close();
        }

        public void DrawTreewithDot()
        {
            DrawTreewithDot("ASTtree.dot");
        }

        public List<string> FindAllPipeNode()
        {
            List<string> pipeNodeList = new List<string>();

            foreach (AstNode node in nodeList)
            {
                if (node.ast.GetType().ToString() == "System.Management.Automation.Language.PipelineAst")
                    pipeNodeList.Add(node.GetScript());
            }

            return pipeNodeList;
        }

        // 遍历所有子节点，获取其中类型为PipeNode的Ast节点
        public static Queue<AstNode> FindAllPipeNodeInSubTree(AstNode root)
        {
            Queue<AstNode> pipeQueue = new Queue<AstNode>();
            Queue<AstNode> childQueue = new Queue<AstNode>();

            childQueue.Enqueue(root);
            AstNode node;
            while (childQueue.Count != 0)
            {
                node = childQueue.Dequeue();
                if (node.type == AstNode.NodeType.pipeNode)
                {
                    pipeQueue.Enqueue(node);
                }
                foreach (var child in node.childList)
                {
                    childQueue.Enqueue(child);
                }
            }

            return pipeQueue;
        }

        public static AstData Tree2Feature(AstTree tree)
        {
            return Tree2Feature(tree.root);
        }

        // 在自定义的AstNode中(AstTree对应的AstNode)计算这部分Node的特征
        public static AstData Tree2Feature(AstNode node)
        {
            string command = "";
            if (node.updatedCommand.Length != 0)
                command = node.command;
            else if (node.command.Length != 0)
                command = node.command;
            else
                command = node.ast.Extent.ToString();

            AstData data = new AstData(new Script2Vector(command).ToAStString());

            return data;
        }

        // For PipeSubTree
        // deprecated，并没有通过这种方法来获取command而是直接通过text来获取command
        public string GetCommand(AstNode node)
        {
            StringBuilder sb = new StringBuilder();

            // Deep-First Traversal
            Stack<AstNode> nodeStack = new Stack<AstNode>();
            nodeStack.Push(node);
            AstNode top;
            while (nodeStack.Count != 0)
            {
                top = nodeStack.Pop();
                if (top.HasChild())
                {
                    for (int i = top.childList.Count() - 1; i >= 0; i--)
                    {
                        nodeStack.Push(top.childList[i]);
                    }
                }
                else
                {
                    sb.Append(top.command);
                }
            }

            return sb.ToString();
        }

        // 递归处理返回其子节点中所有PipeNode的数量
        public int CountPipeNode(AstNode node)
        {
            int count = 0;

            foreach (var n in node.childList)
            {
                count += CountPipeNode(n);
            }

            node.pipeNodeCount = count;
            if (node.type == AstNode.NodeType.pipeNode)
            {
                count++;
            }

            return count;
        }

        // 对node的所有子节点进行清理，只保留当前节点（当该节点的所有子PipeNode节点都已被审查之后会调用这个函数)
        public void Shrink(AstNode node)
        {
            List<AstNode> tempList = new List<AstNode>(node.childList);
            foreach (var n in tempList)
            {
                RemoveSubTree(node, n);
            }

            node.childList.Clear();
        }

        // 对于子节点不包含PipeNode的节点，其子节点可以被忽略，只用保留该节点的信息即可
        public void CompressTree(AstNode root)
        {
            if (root.pipeNodeCount == 0)
            {
                Shrink(root);
            }
            else
            {
                foreach (var child in root.childList)
                {
                    CompressTree(child);
                }
            }
        }

        // 将pipeNode节点的指令以 string 形式进行存储
        public void InitCommands(AstNode root)
        {
            if (root.pipeNodeCount > 0 || root.type == AstNode.NodeType.pipeNode)

                root.command = root.ToString();
            foreach (var child in root.childList)
            {
                InitCommands(child);
            }
        }

        // 计算每个AST节点对应其子节点的PipeNode节点数量
        // 将pipeNode节点的指令以 string 形式进行存储
        // 对于子节点不包含PipeNode的节点，其子节点可以被忽略，只用保留该节点的信息即可
        public void InitPipeSubTree()
        {
            CountPipeNode(root);
            InitCommands(root);
            CompressTree(root);

            type = TreeType.pipeSubTree;
        }
    }
}

