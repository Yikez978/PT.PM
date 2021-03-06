﻿using PT.PM.Common.Nodes.Tokens;

namespace PT.PM.Common.Nodes.Expressions
{
    public class CastExpression : Expression
    {
        public override NodeType NodeType => NodeType.CastExpression;

        public TypeToken Type { get; set; }

        public Expression Expression { get; set; }

        public CastExpression(TypeToken type, Expression expression, TextSpan textSpan, FileNode fileNode)
            : base(textSpan, fileNode)
        {
            Type = type;
            Expression = expression;
        }

        public CastExpression()
        {
        }

        public override UstNode[] GetChildren()
        {
            return new UstNode[] {Type, Expression};
        }

        public override string ToString()
        {
            return $"({Type}){Expression}";
        }
    }
}
