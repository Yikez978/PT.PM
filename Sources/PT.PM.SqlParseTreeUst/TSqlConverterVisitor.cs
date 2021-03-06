﻿using PT.PM.AntlrUtils;
using PT.PM.Common;
using PT.PM.Common.Nodes;
using PT.PM.Common.Nodes.Tokens;
using Antlr4.Runtime.Tree;
using System;
using System.Collections.Generic;
using System.Linq;
using Antlr4.Runtime.Misc;
using PT.PM.Common.Nodes.Statements;
using PT.PM.Common.Nodes.Expressions;
using PT.PM.Common.Nodes.Statements.TryCatchFinally;
using PT.PM.Common.Nodes.Collections;
using PT.PM.Common.Nodes.Statements.Switch;
using PT.PM.Common.Nodes.TypeMembers;
using PT.PM.Common.Nodes.Tokens.Literals;
using PT.PM.TSqlParseTreeUst;
using Antlr4.Runtime;

namespace PT.PM.SqlParseTreeUst
{
    public partial class TSqlConverterVisitor : AntlrDefaultVisitor, ITSqlParserVisitor<UstNode>
    {
        public TSqlConverterVisitor(string fileName, string fileData)
            : base(fileName, fileData)
        {
        }

        public UstNode VisitTsql_file([NotNull] TSqlParser.Tsql_fileContext context)
        {
            FileNode.Root = new BlockStatement(context.batch().Select(b => (Statement)Visit(b)).ToArray(), context.GetTextSpan(), FileNode);
            return FileNode;
        }

        public UstNode VisitBatch([NotNull] TSqlParser.BatchContext context)
        {
            Statement[] sqlClauses = GetStatements(context.sql_clauses());
            var result = new BlockStatement(sqlClauses, context.GetTextSpan(), FileNode);
            return result;
        }

        public UstNode VisitSql_clauses([NotNull] TSqlParser.Sql_clausesContext context)
        {
            return VisitChildren(context);
        }

        /// <returns><see cref="Statement"/></returns>
        public UstNode VisitSql_clause([NotNull] TSqlParser.Sql_clauseContext context)
        {
            var result = Visit(context.GetChild(0));
            if (!(result is Statement))
            {
                result = new WrapperStatement(result);
            }
            return result;
        }

        public UstNode VisitDml_clause([NotNull] TSqlParser.Dml_clauseContext context)
        {
            return Visit(context.children[0]);
        }

        public UstNode VisitDdl_clause([NotNull] TSqlParser.Ddl_clauseContext context)
        {
            return Visit(context.children[0]);
        }

        /// <returns><see cref="Statement"/></returns>
        public UstNode VisitCfl_statement([NotNull] TSqlParser.Cfl_statementContext context)
        {
            return VisitChildren(context);
        }

        /// <returns><see cref="Statement"/></returns>
        public UstNode VisitAnother_statement([NotNull] TSqlParser.Another_statementContext context)
        {
            return Visit(context.children[0]).ToStatementIfRequired();
        }

        #region CFL statements

        /// <returns><see cref="BlockStatement"/></returns>
        public UstNode VisitBlock_statement([NotNull] TSqlParser.Block_statementContext context)
        {
            Statement[] sqlClauses = GetStatements(context.sql_clauses());
            var result = new BlockStatement(sqlClauses, context.GetTextSpan(), FileNode);
            return result;
        }

        /// <returns><see cref="BreakStatement"/></returns>
        public UstNode VisitBreak_statement([NotNull] TSqlParser.Break_statementContext context)
        {
            var result = new BreakStatement(context.GetTextSpan(), FileNode);
            return result;
        }

        /// <returns><see cref="ContinueStatement"/></returns>
        public UstNode VisitContinue_statement([NotNull] TSqlParser.Continue_statementContext context)
        {
            var result = new ContinueStatement(context.GetTextSpan(), FileNode);
            return result;
        }

        /// <returns><see cref="GotoStatement"/></returns>
        public UstNode VisitGoto_statement([NotNull] TSqlParser.Goto_statementContext context)
        {
            var id = (IdToken)Visit(context.id());
            var result = new GotoStatement(id, context.GetTextSpan(), FileNode);
            return result;
        }

        /// <returns><see cref="IfElseStatement"/></returns>
        public UstNode VisitIf_statement([NotNull] TSqlParser.If_statementContext context)
        {
            var condition = (Expression)Visit(context.search_condition());
            var trueStatement = (Statement)Visit(context.sql_clause(0));
            var result = new IfElseStatement(condition, trueStatement, context.GetTextSpan(), FileNode);
            if (context.sql_clause().Length == 2)
            {
                result.FalseStatement = (Statement)Visit(context.sql_clause(1));
            }
            return result;
        }

        /// <returns><see cref="ReturnStatement"/></returns>
        public UstNode VisitReturn_statement([NotNull] TSqlParser.Return_statementContext context)
        {
            Expression expression = null;
            if (context.expression() != null)
            {
                expression = (Expression)Visit(context.expression());
            }
            var result = new ReturnStatement(expression, context.GetTextSpan(), FileNode);
            return result;
        }

        /// <returns><see cref="ThrowStatement"/></returns>
        public UstNode VisitThrow_statement([NotNull] TSqlParser.Throw_statementContext context)
        {
            var exprs = new List<Expression>();
            if (context.throw_error_number() != null)
            {
                exprs.Add((Token)VisitTerminal((ITerminalNode)context.throw_error_number().GetChild(0)));
                exprs.Add((Token)VisitTerminal((ITerminalNode)context.throw_message().GetChild(0)));
                exprs.Add((Token)VisitTerminal((ITerminalNode)context.throw_state().GetChild(0)));
            }
            var result = new ThrowStatement(
                new MultichildExpression(exprs, FileNode),
                context.GetTextSpan(), FileNode);
            return result;
        }

        /// <returns><see cref="TryCatchStatement"/></returns>
        public UstNode VisitTry_catch_statement([NotNull] TSqlParser.Try_catch_statementContext context)
        {
            Statement[] tryClauses = GetStatements(context.try_clauses);
            Statement[] catchClauses = GetStatements(context.catch_clauses);

            var tryBlock = context.try_clauses != null ? new BlockStatement(tryClauses, context.try_clauses.GetTextSpan(), FileNode) : null;
            var tryCatchStatement = new TryCatchStatement(tryBlock, context.GetTextSpan(), FileNode);

            var catchBlock = new BlockStatement(catchClauses,
                context.catch_clauses?.GetTextSpan() ?? context.CATCH(0).GetTextSpan().Union(context.CATCH(1).GetTextSpan()), FileNode);
            tryCatchStatement.CatchClauses = new List<CatchClause>()
            {
                new CatchClause(null, null, catchBlock, catchBlock.TextSpan, FileNode)
            };

            return tryCatchStatement;
        }

        /// <returns><see cref="InvocationExpression"/></returns>
        public UstNode VisitWaitfor_statement([NotNull] TSqlParser.Waitfor_statementContext context)
        {
            return CreateSpecialInvocation(context.WAITFOR(), context, (Expression)Visit(context.expression()));
        }

        /// <returns><see cref="WhileStatement"/></returns>
        public UstNode VisitWhile_statement([NotNull] TSqlParser.While_statementContext context)
        {
            var condition = (Expression)Visit(context.search_condition());
            var statement = (Statement)Visit(context.sql_clause());
            var result = new WhileStatement(condition, statement, context.GetTextSpan(), FileNode);
            return result;
        }

        /// <returns><see cref="ExpressionStatement"/></returns>
        public UstNode VisitPrint_statement([NotNull] TSqlParser.Print_statementContext context)
        {
            InvocationExpression invoke = CreateSpecialInvocation(context.PRINT(), context, (Expression)Visit(context.expression()));
            var result = new ExpressionStatement(invoke);
            return result;
        }

        /// <returns><see cref="ExpressionStatement"/></returns>
        public UstNode VisitRaiseerror_statement([NotNull] TSqlParser.Raiseerror_statementContext context)
        {
            var msg = (Token)ExtractLiteral(context.msg);
            var args = new List<Expression>() { msg,
                (Expression)Visit(context.severity),
                (Expression)Visit(context.state) };
            for (int i = 2; i < context.constant_LOCAL_ID().Length; i++)
                args.Add((Expression)Visit(context.constant_LOCAL_ID()[i]));
            InvocationExpression invoke = CreateSpecialInvocation(context.RAISERROR(), context, args);
            var result = new ExpressionStatement(invoke);
            return result;
        }

        #endregion

        #region DML statements

        /// <returns><see cref="ExpressionStatement"/></returns>
        public UstNode VisitDelete_statement([NotNull] TSqlParser.Delete_statementContext context)
        {
            var exprs = new List<Expression>();

            if (context.with_expression() != null)
            {
                exprs.Add((Expression)Visit(context.with_expression()));
            }
            if (context.expression() != null)
            {
                exprs.Add((Expression)Visit(context.expression()));
            }

            exprs.Add((Expression)Visit(context.delete_statement_from()));

            if (context.insert_with_table_hints() != null)
            {
                exprs.Add((MultichildExpression)Visit(context.insert_with_table_hints()));
            }
            if (context.output_clause() != null)
            {
                exprs.Add((InvocationExpression)Visit(context.output_clause()));
            }
            if (context.table_sources() != null)
            {
                exprs.Add((MultichildExpression)Visit(context.table_sources()));
            }
            if (context.search_condition() != null)
            {
                exprs.Add((Expression)Visit(context.search_condition()));
            }
            if (context.for_clause() != null)
            {
                exprs.Add((InvocationExpression)Visit(context.for_clause()));
            }
            if (context.option_clause() != null)
            {
                exprs.Add((InvocationExpression)Visit(context.option_clause()));
            }

            var result = CreateSpecialInvocation(context.DELETE(), context, exprs);
            return new ExpressionStatement(result);
        }

        /// <returns><see cref="Expression"/></returns>
        public UstNode VisitDelete_statement_from([NotNull] TSqlParser.Delete_statement_fromContext context)
        {
            return (Expression)Visit(context.GetChild(0));
        }

        /// <returns><see cref="MultichildExpression"/></returns>
        public UstNode VisitInsert_with_table_hints([NotNull] TSqlParser.Insert_with_table_hintsContext context)
        {
            var exprs = context.table_hint().Select(hint => (Expression)Visit(hint)).ToList();
            var result = new MultichildExpression(exprs, context.GetTextSpan(), FileNode);
            return result;
        }

        /// <returns><see cref="ExpressionStatement"/></returns>
        public UstNode VisitInsert_statement([NotNull] TSqlParser.Insert_statementContext context)
        {
            var exprs = new List<Expression>();

            if (context.with_expression() != null)
            {
                exprs.Add((Expression)Visit(context.with_expression()));
            }
            if (context.expression() != null)
            {
                exprs.Add((Expression)Visit(context.expression()));
            }

            if (context.ddl_object() != null)
            {
                exprs.Add((Token)Visit(context.ddl_object()));
            }
            else if (context.rowset_function_limited() != null)
            {
                exprs.Add((InvocationExpression)Visit(context.rowset_function_limited()));
            }

            if (context.insert_with_table_hints() != null)
            {
                exprs.Add((MultichildExpression)Visit(context.insert_with_table_hints()));
            }
            if (context.column_name_list() != null)
            {
                exprs.Add(new WrapperExpression((ArgsNode)Visit(context.column_name_list())));
            }
            if (context.output_clause() != null)
            {
                exprs.Add((InvocationExpression)Visit(context.output_clause()));
            }

            exprs.Add((Expression)Visit(context.insert_statement_value()));

            if (context.for_clause() != null)
            {
                exprs.Add((InvocationExpression)Visit(context.for_clause()));
            }
            if (context.option_clause() != null)
            {
                exprs.Add((InvocationExpression)Visit(context.option_clause()));
            }

            var result = CreateSpecialInvocation(context.INSERT(), context, exprs);
            return new ExpressionStatement(result);
        }

        /// <returns><see cref="Expression"/></returns>
        public UstNode VisitInsert_statement_value([NotNull] TSqlParser.Insert_statement_valueContext context)
        {
            if (context.DEFAULT() != null)
            {
                return new IdToken(context.DEFAULT().GetText().ToLowerInvariant() + context.VALUES().GetText().ToLowerInvariant(),
                    context.DEFAULT().GetTextSpan().Union(context.VALUES().GetTextSpan()), FileNode);
            }
            else
            {
                var result = Visit(context.GetChild(0));
                if (!(result is Expression))
                {
                    result = new WrapperExpression(result);
                }
                return result;
            }
        }

        /// <returns><see cref="ExpressionStatement"/></returns>
        public UstNode VisitSelect_statement([NotNull] TSqlParser.Select_statementContext context)
        {
            var exprs = new List<Expression>();

            if (context.with_expression() != null)
            {
                exprs.Add((Expression)Visit(context.with_expression()));
            }
            exprs.Add((Expression)Visit(context.query_expression()));

            if (context.order_by_clause() != null)
            {
                exprs.Add((Expression)Visit(context.order_by_clause()));
            }

            if (context.for_clause() != null)
            {
                exprs.Add((InvocationExpression)Visit(context.for_clause()));
            }

            if (context.option_clause() != null)
            {
                exprs.Add((InvocationExpression)Visit(context.option_clause()));
            }

            var selectLiteral = new IdToken("select", default(TextSpan), FileNode);
            var result = new InvocationExpression(selectLiteral, new ArgsNode(exprs), context.GetTextSpan(), FileNode);
            return new ExpressionStatement(result);
        }

        /// <returns><see cref="ExpressionStatement"/></returns>
        public UstNode VisitUpdate_statement([NotNull] TSqlParser.Update_statementContext context)
        {
            var exprs = new List<Expression>();

            if (context.with_expression() != null)
            {
                exprs.Add((Expression)Visit(context.with_expression()));
            }
            if (context.expression() != null)
            {
                exprs.Add((Expression)Visit(context.expression()));
            }

            if (context.ddl_object() != null)
            {
                exprs.Add((Token)Visit(context.ddl_object()));
            }
            else if (context.rowset_function_limited() != null)
            {
                exprs.Add((InvocationExpression)Visit(context.rowset_function_limited()));
            }

            if (context.with_table_hints() != null)
            {
                exprs.Add((MultichildExpression)Visit(context.with_table_hints()));
            }

            if (context.output_clause() != null)
            {
                exprs.Add((InvocationExpression)Visit(context.output_clause()));
            }

            if (context.table_sources() != null)
            {
                exprs.Add((MultichildExpression)Visit(context.table_sources()));
            }

            if (context.search_condition_list() != null)
            {
                exprs.Add(new WrapperExpression(Visit(context.search_condition_list())));
            }

            if (context.for_clause() != null)
            {
                exprs.Add((InvocationExpression)Visit(context.for_clause()));
            }
            if (context.option_clause() != null)
            {
                exprs.Add((InvocationExpression)Visit(context.option_clause()));
            }

            var result = CreateSpecialInvocation(context.UPDATE(), context, exprs);
            return new ExpressionStatement(result);
        }

        /// <returns><see cref="InvocationExpression"/></returns>
        public UstNode VisitOutput_clause([NotNull] TSqlParser.Output_clauseContext context)
        {
            var exprs = new List<Expression>();
            var multichildren = context.output_dml_list_elem()
                .Select(elem => (Expression)Visit(elem));
            exprs.AddRange(multichildren);
            if (context.column_name_list() != null)
            {
                exprs.Add(new WrapperExpression(
                    (ArgsNode)Visit(context.column_name_list())));
            }

            var result = CreateSpecialInvocation(context.OUTPUT(), context, exprs);
            return result;
        }

        /// <returns><see cref="MultichildExpression"/></returns>
        public UstNode VisitOutput_dml_list_elem([NotNull] TSqlParser.Output_dml_list_elemContext context)
        {
            return VisitChildren(context);
        }

        /// <returns><see cref="IdToken"/></returns>
        public UstNode VisitOutput_column_name([NotNull] TSqlParser.Output_column_nameContext context)
        {
            var result = new IdToken(RemoveSpaces(context.GetText()), context.GetTextSpan(), FileNode);
            return result;
        }

        #endregion

        #region DDL statements

        /// <returns><see cref="ExpressionStatement"/></returns>
        public UstNode VisitCreate_index([NotNull] TSqlParser.Create_indexContext context)
        {
            var funcName = new IdToken((context.CREATE().GetText() + " " + context.INDEX().GetText()).ToLowerInvariant(),
               context.CREATE().GetTextSpan().Union(context.INDEX().GetTextSpan()), FileNode);
            var exprs = new List<Expression>();
            if (context.id().Length > 0)
            {
                exprs.Add((IdToken)Visit(context.id(0)));
            }
            exprs.Add((Expression)Visit(context.table_name_with_hint()));
            if (context.column_name_list() != null)
            {
                exprs.Add(new WrapperExpression((ArgsNode)Visit(context.column_name_list())));
            }
            var invocation = new InvocationExpression(funcName, new ArgsNode(exprs), context.GetTextSpan(), FileNode);
            var result = new ExpressionStatement(invocation);
            return result;
        }

        /// <returns><see cref="MethodDeclaration"/></returns>
        public UstNode VisitCreate_or_alter_procedure([NotNull] TSqlParser.Create_or_alter_procedureContext context)
        {
            var exprs = new List<Expression>();
            exprs.AddRange(context.procedure_option().Select(opt => (Expression)Visit(opt)).ToArray());

            var id = (IdToken)Visit(context.func_proc_name());
            var body = new BlockStatement(
                context.sql_clauses().sql_clause().Select(clause => (Statement)Visit(clause)).ToArray(),
                FileNode);
            ParameterDeclaration[] parameters = context.procedure_param()
                .Select(param => (ParameterDeclaration)Visit(param)).ToArray();

            var result = new MethodDeclaration(id, parameters, body, context.GetTextSpan(), FileNode);
            return result;
        }

        /// <returns><see cref="ParameterDeclaration"/></returns>
        public UstNode VisitProcedure_param([NotNull] TSqlParser.Procedure_paramContext context)
        {
            var type = (TypeToken)Visit(context.data_type());
            var id = (IdToken)Visit(context.LOCAL_ID());
            var result = new ParameterDeclaration(type, id, context.GetTextSpan(), FileNode);
            return result;
        }

        /// <returns><see cref="Expression"/></returns>
        public UstNode VisitProcedure_option([NotNull] TSqlParser.Procedure_optionContext context)
        {
            Expression result;
            if (context.execute_clause() != null)
            {
                result = (Expression)Visit(context.execute_clause());
            }
            else
            {
                result = new IdToken(context.GetChild(0).GetText(), context.GetTextSpan(), FileNode);
            }
            return result;
        }

        public UstNode VisitCreate_statistics([NotNull] TSqlParser.Create_statisticsContext context)
        {
            var funcName = new IdToken((context.CREATE().GetText() + " " + context.STATISTICS().GetText()).ToLowerInvariant(),
               context.CREATE().GetTextSpan().Union(context.STATISTICS().GetTextSpan()), FileNode);
            var exprs = new List<Expression>();
            exprs.Add((IdToken)Visit(context.id()));
            exprs.Add((Expression)Visit(context.table_name_with_hint()));
            exprs.Add(new WrapperExpression((ArgsNode)Visit(context.column_name_list())));
            var invocation = new InvocationExpression(funcName, new ArgsNode(exprs), context.GetTextSpan(), FileNode);
            var result = new ExpressionStatement(invocation);
            return result;
        }

        /// <returns><see cref="ExpressionStatement"/></returns>
        public UstNode VisitCreate_table([NotNull] TSqlParser.Create_tableContext context)
        {
            var funcName = new IdToken((context.CREATE().GetText() + " " + context.TABLE().GetText()).ToLowerInvariant(),
               context.CREATE().GetTextSpan().Union(context.TABLE().GetTextSpan()), FileNode);
            var exprs = new List<Expression>();
            exprs.Add((IdToken)Visit(context.table_name()));
            exprs.Add(new WrapperExpression((ArgsNode)Visit(context.column_def_table_constraints())));
            if (context.id().Length > 0)
            {
                exprs.Add((IdToken)Visit(context.id(0)));
            }
            var invocation = new InvocationExpression(funcName, new ArgsNode(exprs), context.GetTextSpan(), FileNode);
            var result = new ExpressionStatement(invocation);
            return result;
        }

        /// <returns><see cref="ExpressionStatement"/></returns>
        public UstNode VisitCreate_view([NotNull] TSqlParser.Create_viewContext context)
        {
            var funcName = new IdToken((context.CREATE().GetText() + " " + context.VIEW().GetText()).ToLowerInvariant(),
               context.CREATE().GetTextSpan().Union(context.VIEW().GetTextSpan()), FileNode);
            var exprs = new List<Expression>();
            exprs.Add((IdToken)Visit(context.simple_name()));
            if (context.column_name_list() != null)
            {
                exprs.Add(new WrapperExpression((ArgsNode)Visit(context.column_name_list())));
            }
            exprs.AddRange(context.view_attribute().Select(attr => (IdToken)Visit(attr)));
            exprs.Add(new WrapperExpression((Statement)Visit(context.select_statement())));
            var invocation = new InvocationExpression(funcName, new ArgsNode(exprs), context.GetTextSpan(), FileNode);
            var result = new ExpressionStatement(invocation);
            return result;
        }

        /// <returns><see cref="IdToken"/></returns>
        public UstNode VisitView_attribute([NotNull] TSqlParser.View_attributeContext context)
        {
            var result = new IdToken(context.GetText(), context.GetTextSpan(), FileNode);
            return result;
        }

        /// <returns><see cref="ExpressionStatement"/></returns>
        public UstNode VisitAlter_table([NotNull] TSqlParser.Alter_tableContext context)
        {
            var funcName = new IdToken((context.ALTER(0).GetText() + " " + context.TABLE(0).GetText()).ToLowerInvariant(),
                context.ALTER(0).GetTextSpan().Union(context.TABLE(0).GetTextSpan()), FileNode);
            var exprs = new List<Expression>();
            var tableName = (IdToken)Visit(context.table_name(0));
            exprs.Add(tableName);
            if (context.column_def_table_constraint() != null)
            {
                exprs.Add((Expression)Visit(context.column_def_table_constraint()));
            }
            var invocation = new InvocationExpression(tableName, new ArgsNode(exprs), context.GetTextSpan(), FileNode);
            var result = new ExpressionStatement(invocation);
            return result;
        }

        /// <returns><see cref="ExpressionStatement"/></returns>
        public UstNode VisitAlter_database([NotNull] TSqlParser.Alter_databaseContext context)
        {
            var funcName = new IdToken((context.ALTER().GetText() + " " + context.DATABASE().GetText()).ToLowerInvariant(),
                context.ALTER().GetTextSpan().Union(context.DATABASE().GetTextSpan()), FileNode);

            IdToken id;
            if (context.database != null)
            {
                id = (IdToken)Visit(context.database);
            }
            else
            {
                id = new IdToken(context.CURRENT().GetText().ToLowerInvariant(), context.CURRENT().GetTextSpan(), FileNode);
            }
            var invocation = new InvocationExpression(funcName, new ArgsNode(id), context.GetTextSpan(), FileNode);
            return new ExpressionStatement(invocation);
        }

        /// <returns><see cref="IdToken"/></returns>
        public UstNode VisitDatabase_optionspec([NotNull] TSqlParser.Database_optionspecContext context)
        {
            return VisitChildren(context);
        }

        /// <returns><see cref="ExpressionStatement"/></returns>
        public UstNode VisitDrop_index([NotNull] TSqlParser.Drop_indexContext context)
        {
            return VisitChildren(context);
        }

        /// <returns><see cref="ExpressionStatement"/></returns>
        public UstNode VisitDrop_procedure([NotNull] TSqlParser.Drop_procedureContext context)
        {
            var funcName = new IdToken((context.DROP().GetText() + " " + context.PROCEDURE().GetText()).ToLowerInvariant(),
                context.DROP().GetTextSpan().Union(context.PROCEDURE().GetTextSpan()), FileNode);
            var procName = (IdToken)Visit(context.func_proc_name(0));
            var invocation = new InvocationExpression(funcName, new ArgsNode(procName), context.GetTextSpan(), FileNode);
            return new ExpressionStatement(invocation);
        }

        /// <returns><see cref="ExpressionStatement"/></returns>
        public UstNode VisitDrop_statistics([NotNull] TSqlParser.Drop_statisticsContext context)
        {
            var funcName = new IdToken((context.DROP().GetText() + " " + context.STATISTICS().GetText()).ToLowerInvariant(),
                context.DROP().GetTextSpan().Union(context.STATISTICS().GetTextSpan()), FileNode);
            var id = (IdToken)Visit(context.id());
            var invocation = new InvocationExpression(funcName, new ArgsNode(id), context.GetTextSpan(), FileNode);
            return new ExpressionStatement(invocation);
        }

        /// <returns><see cref="ExpressionStatement"/></returns>
        public UstNode VisitDrop_table([NotNull] TSqlParser.Drop_tableContext context)
        {
            var funcName = new IdToken((context.DROP().GetText() + " " + context.TABLE().GetText()).ToLowerInvariant(),
                context.DROP().GetTextSpan().Union(context.TABLE().GetTextSpan()), FileNode);
            var tableName = (IdToken)Visit(context.table_name());
            var invocation = new InvocationExpression(funcName, new ArgsNode(tableName), context.GetTextSpan(), FileNode);
            return new ExpressionStatement(invocation);
        }

        /// <returns><see cref="ExpressionStatement"/></returns>
        public UstNode VisitDrop_view([NotNull] TSqlParser.Drop_viewContext context)
        {
            var exprs = context.simple_name().Select(name => (IdToken)Visit(name)).ToArray();
            var funcName = new IdToken((context.DROP().GetText() + " " + context.VIEW().GetText()).ToLowerInvariant(),
                context.DROP().GetTextSpan().Union(context.VIEW().GetTextSpan()), FileNode);
            var invocation = new InvocationExpression(funcName, new ArgsNode(exprs), context.GetTextSpan(), FileNode);
            return new ExpressionStatement(invocation);
        }

        /// <returns><see cref="InvocationExpression"/></returns>
        public UstNode VisitRowset_function_limited([NotNull] TSqlParser.Rowset_function_limitedContext context)
        {
            return (InvocationExpression)Visit(context.GetChild(0));
        }

        /// <returns><see cref="InvocationExpression"/></returns>
        public UstNode VisitOpenquery([NotNull] TSqlParser.OpenqueryContext context)
        {
            return CreateSpecialInvocation(context.OPENQUERY(), context,
                new List<Expression> { (IdToken)Visit(context.id()), ExtractLiteral(context.query) });
        }

        /// <returns><see cref="InvocationExpression"/></returns>
        public UstNode VisitOpendatasource([NotNull] TSqlParser.OpendatasourceContext context)
        {
            return CreateSpecialInvocation(context.OPENDATASOURCE(), context, new List<Expression> {
                ExtractLiteral(context.provider), ExtractLiteral(context.init),
                (IdToken)Visit(context.database), (IdToken)Visit(context.scheme),
                (IdToken)Visit(context.scheme) });
        }

        #endregion

        #region Other statements

        /// <returns><see cref="Statement"/></returns>
        public UstNode VisitDeclare_statement([NotNull] TSqlParser.Declare_statementContext context)
        {
            Statement result;
            if (context.LOCAL_ID() != null)
            {
                TextSpan textSpan;
                Expression expression;
                if (context.table_type_definition() != null)
                {
                    expression = (Expression)Visit(context.table_type_definition());
                    textSpan = context.table_type_definition().GetTextSpan();
                }
                else
                {
                    expression = (Expression)Visit(context.xml_type_definition());
                    textSpan = context.xml_type_definition().GetTextSpan();
                }
                var assignment = new AssignmentExpression((Token)Visit(context.LOCAL_ID()), expression,
                    textSpan, FileNode);
                result = new ExpressionStatement(
                    new VariableDeclarationExpression(new TypeToken("TABLE", default(TextSpan), FileNode),
                    new[] { assignment }, context.GetTextSpan(), FileNode));
            }
            else
            {
                if (context.declare_local().Length == 1)
                {
                    result = new ExpressionStatement(
                        (VariableDeclarationExpression)Visit(context.declare_local(0)),
                        context.GetTextSpan(), FileNode);
                }
                else
                {
                    Statement[] statements = context.declare_local()
                        .Select(local => new ExpressionStatement(
                         (VariableDeclarationExpression)Visit(local))).ToArray();
                    result = new BlockStatement(statements, context.GetTextSpan(), FileNode);
                }
            }
            return result;
        }

        /// <returns><see cref="Statement"/></returns>
        public UstNode VisitCursor_statement([NotNull] TSqlParser.Cursor_statementContext context)
        {
            Statement result;
            if (context.fetch_cursor() != null)
            {
                result = (Statement)Visit(context.fetch_cursor());
            }
            else if (context.declare_cursor() != null)
            {
                result = (Statement)Visit(context.declare_cursor());
            }
            else
            {
                var first = context.GetChild<ITerminalNode>(0);
                var funcName = new IdToken(first.GetText(), first.GetTextSpan(), FileNode);
                var args = new ArgsNode((Token)Visit(context.cursor_name()));
                var invocation = new InvocationExpression(funcName, args, context.GetTextSpan(), FileNode);
                result = new ExpressionStatement(invocation);
            }
            return result;
        }

        /// <returns><see cref="Statement"/></returns>
        public UstNode VisitExecute_statement([NotNull] TSqlParser.Execute_statementContext context)
        {
            var first = context.GetChild<ITerminalNode>(0);
            var argsNode = new ArgsNode();
            Expression expr;
            if (context.func_proc_name() != null)
            {
                var funcName = (IdToken)Visit(context.func_proc_name());
                for (int i = 0; i < context.execute_statement_arg().Length; i++)
                {
                    argsNode.Collection.Add((Expression)Visit(context.execute_statement_arg(i)));
                }
                expr = new InvocationExpression(funcName, argsNode, context.GetTextSpan(), FileNode);
            }
            else
            {
                var executeName = new IdToken("exec", first.GetTextSpan(), FileNode);
                expr = (Expression)Visit(context.execute_var_string(0));
                for (int i = 1; i < context.execute_var_string().Length; i++)
                {
                    var right = (Expression)Visit(context.execute_var_string(i));
                    var binatyOpLiteral = new BinaryOperatorLiteral(BinaryOperator.Plus,
                        context.execute_var_string(i).GetTextSpan(), FileNode);
                    expr = new BinaryOperatorExpression(expr, binatyOpLiteral, right,
                        context.execute_var_string(0).GetTextSpan().Union(
                            context.execute_var_string(i).GetTextSpan()), FileNode);
                }
                argsNode.Collection.Add(expr);
                expr = new InvocationExpression(executeName, argsNode, context.GetTextSpan(), FileNode);
            }
            var result = new ExpressionStatement(expr);
            return result;
        }

        /// <returns><see cref="Expression"/></returns>
        public UstNode VisitExecute_statement_arg([NotNull] TSqlParser.Execute_statement_argContext context)
        {
            Expression result;
            if (context.constant_LOCAL_ID() != null)
            {
                result = (Token)Visit(context.constant_LOCAL_ID());
            }
            else if (context.id() != null)
            {
                result = (Expression)Visit(context.id());
            }
            else
            {
                var lastTerminal = context.GetChild<ITerminalNode>(context.ChildCount - 1);
                result = new IdToken(lastTerminal.GetText(), lastTerminal.GetTextSpan(), FileNode);
            }

            if (context.parameter != null)
            {
                Token left = ExtractLiteral(context.parameter);
                result = new AssignmentExpression(left, result, context.GetTextSpan(), FileNode);
            }
            return result;
        }

        /// <returns><see cref="Token"/></returns>
        public UstNode VisitExecute_var_string([NotNull] TSqlParser.Execute_var_stringContext context)
        {
            return (Token)Visit(context.GetChild<ITerminalNode>(0));
        }

        /// <returns><see cref="Statement"/></returns>
        public UstNode VisitSecurity_statement([NotNull] TSqlParser.Security_statementContext context)
        {
            Expression expr;
            if (context.execute_clause() != null)
            {
                expr = (Expression)Visit(context.execute_clause());
            }
            else if (context.GRANT().Length > 0)
            {
                string str = context.GRANT(0).GetText().ToLowerInvariant();
                if (context.ALL() != null)
                {
                    str += "_" + context.ALL().GetText().ToLowerInvariant();
                }
                var funcName = new IdToken(str, context.GetTextSpan(), FileNode);
                var args = new ArgsNode();
                expr = new InvocationExpression(funcName, args, context.GetTextSpan(), FileNode);
            }
            else if (context.REVERT() != null)
            {
                var args = new ArgsNode();
                if (context.LOCAL_ID() != null)
                {
                    args.Collection.Add((Token)Visit(context.LOCAL_ID()));
                }
                expr = new InvocationExpression(
                    new IdToken(context.REVERT().GetText(), context.REVERT().GetTextSpan(), FileNode),
                    new ArgsNode(), context.GetTextSpan(), FileNode);
            }
            else
            {
                return new WrapperStatement(Visit(context.GetChild(0)));
            }
            var result = new ExpressionStatement(expr);
            return result;
        }

        /// <returns><see cref="Statement"/></returns>
        public UstNode VisitGrant_permission([NotNull] TSqlParser.Grant_permissionContext context)
        {
            return VisitChildren(context);
        }

        /// <returns><see cref="Statement"/></returns>
        public UstNode VisitSet_statement([NotNull] TSqlParser.Set_statementContext context)
        {
            Statement result = null;
            if (context.LOCAL_ID() != null)
            {
                var localId = (Token)Visit(context.LOCAL_ID());
                if (context.assignment_operator() != null)
                {
                    var binaryOpLiteral = (BinaryOperatorLiteral)Visit(context.assignment_operator());
                    result = new ExpressionStatement(
                        new BinaryOperatorExpression(localId, binaryOpLiteral,
                        (Expression)Visit(context.expression()), context.GetTextSpan(), FileNode));
                }
                else
                {
                    if (context.expression() != null)
                    {
                        Expression left = localId;
                        if (context.member_name != null)
                        {
                            left = new MemberReferenceExpression(localId, (IdToken)Visit(context.member_name),
                                context.LOCAL_ID().GetTextSpan().Union(context.member_name.GetTextSpan()), FileNode);
                        }
                        result = new ExpressionStatement(
                            new AssignmentExpression(left, (Expression)Visit(context.expression()),
                            context.GetTextSpan(), FileNode));
                    }
                    else if (context.declare_set_cursor_common() != null)
                    {
                        result = new ExpressionStatement((Expression)Visit(context.declare_set_cursor_common()));
                    }
                }
            }
            else
            {
                result = (Statement)Visit(context.set_special());
            }
            return result;
        }

        /// <returns><see cref="ExpressionStatement"/></returns>
        public UstNode VisitTransaction_statement([NotNull] TSqlParser.Transaction_statementContext context)
        {
            Token id;
            var args = new ArgsNode();
            if (context.id() != null)
            {
                id = (IdToken)Visit(context.id());
                args.Collection.Add(id);
            }
            else if (context.LOCAL_ID() != null)
            {
                id = (Token)Visit(context.LOCAL_ID());
                args.Collection.Add(id);
            }
            var first = context.GetChild<ITerminalNode>(0);
            var functionName = new IdToken(first.GetText(), first.GetTextSpan(), FileNode);
            return new ExpressionStatement(new InvocationExpression(functionName, args, context.GetTextSpan(), FileNode));
        }

        /// <returns><see cref="ExpressionStatement"/></returns>
        public UstNode VisitGo_statement([NotNull] TSqlParser.Go_statementContext context)
        {
            return new ExpressionStatement(CreateSpecialInvocation(context.GO(), context, new List<Expression>()));
        }

        /// <returns><see cref="ExpressionStatement"/></returns>
        public UstNode VisitUse_statement([NotNull] TSqlParser.Use_statementContext context)
        {
            var database = (IdToken)Visit(context.id());
            return new ExpressionStatement(CreateSpecialInvocation(context.USE(), context, database));
        }

        /// <returns><see cref="Expression"/></returns>
        public UstNode VisitExecute_clause([NotNull] TSqlParser.Execute_clauseContext context)
        {
            var first = context.GetChild<ITerminalNode>(0);
            var executeName = new IdToken(first.GetText(), first.GetTextSpan(), FileNode);
            return new InvocationExpression(executeName, new ArgsNode(new Expression[]
                { new IdToken(context.clause.Text, context.clause.GetTextSpan(), FileNode) }),
                  context.GetTextSpan(), FileNode);
        }

        /// <returns><see cref="VariableDeclarationExpression"/></returns>
        public UstNode VisitDeclare_local([NotNull] TSqlParser.Declare_localContext context)
        {
            var type = (TypeToken)Visit(context.data_type());
            var variable = (Token)Visit(context.LOCAL_ID());
            var initExpr = context.expression() != null ? (Expression)Visit(context.expression()) : null;
            var assignment = new AssignmentExpression(variable, initExpr,
                type.TextSpan.Union(initExpr?.TextSpan ?? default(TextSpan)), FileNode);
            var result = new VariableDeclarationExpression(type, new[] { assignment }, context.GetTextSpan(), FileNode);
            return result;
        }

        /// <returns><see cref="ObjectCreateExpression"/></returns>
        public UstNode VisitTable_type_definition([NotNull] TSqlParser.Table_type_definitionContext context)
        {
            var type = new TypeToken(context.TABLE().GetText(), context.TABLE().GetTextSpan(), FileNode);
            var argsNode = (ArgsNode)Visit(context.column_def_table_constraints());
            var result = new ObjectCreateExpression(type, argsNode, context.GetTextSpan(), FileNode);
            return result;
        }

        /// <returns><see cref="ArgsNode"/></returns>
        public UstNode VisitColumn_def_table_constraints([NotNull] TSqlParser.Column_def_table_constraintsContext context)
        {
            var result = new ArgsNode(context.column_def_table_constraint()
                .Select(def => (Expression)Visit(def)).ToArray());
            return result;
        }

        /// <returns><see cref="Expression"/></returns>
        public UstNode VisitColumn_def_table_constraint([NotNull] TSqlParser.Column_def_table_constraintContext context)
        {
            Expression result;
            if (context.column_definition() != null)
            {
                result = new WrapperExpression((FieldDeclaration)Visit(context.column_definition()));
            }
            else
            {
                result = (MultichildExpression)Visit(context.table_constraint());
            }
            return result;
        }

        /// <returns><see cref="FieldDeclaration"/></returns>
        public UstNode VisitColumn_definition([NotNull] TSqlParser.Column_definitionContext context)
        {
            Expression right = null;
            TextSpan assignmentTextSpan = context.id(0).GetTextSpan();
            if (context.expression() != null)
            {
                right = (Expression)Visit(context.expression());
                assignmentTextSpan = assignmentTextSpan.Union(right.TextSpan);
            }

            Expression[] constraints = context.column_constraint()
                .Select(constraint => (Expression)Visit(constraint)).ToArray();
            if (constraints.Length > 0)
            {
                if (right != null)
                {
                    right = new MultichildExpression(new List<Expression>(constraints) { right }, FileNode);
                }
                else
                {
                    right = new MultichildExpression(new List<Expression>(constraints), FileNode);
                }
            }

            var assignment = new AssignmentExpression((IdToken)Visit(context.id(0)), right, assignmentTextSpan, FileNode);
            var result = new FieldDeclaration(new[] { assignment }, context.GetTextSpan(), FileNode);
            return result;
        }

        /// <returns><see cref="MultichildExpression"/></returns>
        public UstNode VisitColumn_constraint([NotNull] TSqlParser.Column_constraintContext context)
        {
            var exprs = new List<Expression>();
            if (context.index_options() != null)
            {
                exprs.AddRange(((ArgsNode)Visit(context.index_options())).Collection);
            }

            if (context.search_condition() != null)
            {
                exprs.Add((Expression)Visit(context.search_condition()));
            }
            return new MultichildExpression(exprs, context.GetTextSpan(), FileNode);
        }

        /// <returns><see cref="MultichildExpression"/></returns>
        public UstNode VisitTable_constraint([NotNull] TSqlParser.Table_constraintContext context)
        {
            var exprs = new List<Expression>();
            if (context.index_options() != null)
            {
                exprs.AddRange(((ArgsNode)Visit(context.index_options())).Collection);
            }

            if (context.search_condition() != null)
            {
                exprs.Add((Expression)Visit(context.search_condition()));
            }
            return new MultichildExpression(exprs, context.GetTextSpan(), FileNode);
        }

        /// <returns><see cref="ArgsNode"/></returns>
        public UstNode VisitIndex_options([NotNull] TSqlParser.Index_optionsContext context)
        {
            Expression[] options = context.index_option().Select(option => (Expression)Visit(option))
                .ToArray();
            return new ArgsNode(options);
        }

        /// <returns><see cref="AssignmentExpression"/></returns>
        public UstNode VisitIndex_option([NotNull] TSqlParser.Index_optionContext context)
        {
            var left = (IdToken)Visit(context.simple_id(0));
            Token right;
            if (context.simple_id().Length > 1)
            {
                right = new IdToken(context.simple_id(1).GetText(), context.simple_id(1).GetTextSpan(), FileNode);
            }
            else if (context.on_off() != null)
            {
                right = (BooleanLiteral)Visit(context.on_off());
            }
            else
            {
                right = new IntLiteral(long.Parse(context.DECIMAL().GetText()), context.DECIMAL().Symbol.GetTextSpan(), FileNode);
            }

            var result = new AssignmentExpression(left, right, context.GetTextSpan(), FileNode);
            return result;
        }

        /// <returns><see cref="ExpressionStatement"/></returns>
        public UstNode VisitDeclare_cursor([NotNull] TSqlParser.Declare_cursorContext context)
        {
            var declareId = new IdToken(context.DECLARE().GetText().ToLowerInvariant() + "_" +
                 context.CURSOR().GetText().ToLowerInvariant(), context.GetTextSpan(), FileNode);
            var cursorName = (Token)Visit(context.cursor_name());
            var exprs = new List<Expression>() { cursorName };
            if (context.declare_set_cursor_common() != null)
            {
                exprs.Add((InvocationExpression)Visit(context.declare_set_cursor_common()));
            }
            if (context.column_name_list() != null)
            {
                exprs.AddRange(((ArgsNode)Visit(context.column_name_list())).Collection);
            }
            if (context.select_statement() != null)
            {
                exprs.Add(new WrapperExpression((Statement)Visit(context.select_statement())));
            }
            var invocation = new InvocationExpression(declareId, new ArgsNode(exprs), context.GetTextSpan(), FileNode);
            var result = new ExpressionStatement(invocation);
            return result;
        }

        /// <returns><see cref="InvocationExpression"/></returns>
        public UstNode VisitDeclare_set_cursor_common([NotNull] TSqlParser.Declare_set_cursor_commonContext context)
        {
            var selectStatement = (Statement)Visit(context.select_statement());
            var argsNode = new ArgsNode(new WrapperExpression(selectStatement));
            var funcName = string.Join(" ", context.children.Take(context.ChildCount - 1).Select(c => c.GetText()));
            var funcNameLiteral = new IdToken(funcName, context.GetTextSpan(), FileNode);
            var result = new InvocationExpression(funcNameLiteral, argsNode, context.GetTextSpan(), FileNode);
            return result;
        }

        /// <returns><see cref="ExpressionStatement"/></returns>
        public UstNode VisitFetch_cursor([NotNull] TSqlParser.Fetch_cursorContext context)
        {
            var exprs = new List<Expression>();
            if (context.expression() != null)
            {
                exprs.Add((Expression)Visit(context.expression()));
            }
            var invocation = CreateSpecialInvocation(context.FETCH(), context, exprs);
            var result = new ExpressionStatement(invocation);
            return result;
        }

        /// <returns><see cref="ExpressionStatement"/></returns>
        public UstNode VisitSet_special([NotNull] TSqlParser.Set_specialContext context)
        {
            var funcName = new IdToken(context.SET().GetText().ToLowerInvariant(), context.GetTextSpan(), FileNode);
            var invocation = new InvocationExpression(funcName, new ArgsNode(), context.GetTextSpan(), FileNode);
            var result = new ExpressionStatement(invocation);
            return result;
        }

        /// <returns><see cref="Token"/></returns>
        public UstNode VisitConstant_LOCAL_ID([NotNull] TSqlParser.Constant_LOCAL_IDContext context)
        {
            Token result;
            if (context.constant() != null)
            {
                result = (Token)Visit(context.constant());
            }
            else
            {
                result = (Token)Visit(context.LOCAL_ID());
            }
            return result;
        }

        #endregion

        #region Expressions

        /// <returns><see cref="Expression"/></returns>
        public UstNode VisitExpression([NotNull] TSqlParser.ExpressionContext context)
        {
            if (context.expression().Length == 2)
            {
                var expr1 = (Expression)Visit(context.expression(0));
                var expr2 = (Expression)Visit(context.expression(1));
                var opText = RemoveSpaces(context.GetChild(1).GetText());
                if (opText == "=")
                {
                    opText = "==";
                }
                BinaryOperator op;
                if (!BinaryOperatorLiteral.TextBinaryOperator.TryGetValue(opText, out op))
                {
                    op = BinaryOperator.Equal;
                }
                var opLiteral = new BinaryOperatorLiteral(op, context.GetTextSpan(), FileNode);
                var result = new BinaryOperatorExpression(expr1, opLiteral, expr2, context.GetTextSpan(), FileNode);
                return result;
            }

            return VisitChildren(context);
        }

        /// <returns><see cref="IdToken"/></returns>
        public UstNode VisitPrimitive_expression([NotNull] TSqlParser.Primitive_expressionContext context)
        {
            Token result;
            if (context.DEFAULT() != null)
            {
                result = new IdToken(context.GetText(), context.GetTextSpan(), FileNode);
            }
            else if (context.NULL() != null)
            {
                result = new NullLiteral(context.GetTextSpan(), FileNode);
            }
            else if (context.LOCAL_ID() != null)
            {
                result = new IdToken(context.GetText().Substring(1), context.GetTextSpan(), FileNode);
            }
            else // constant
            {
                result = (Token)Visit(context.constant());
            }
            return result;
        }

        /// <returns><see cref="WrapperExpression"/></returns>
        public UstNode VisitCase_expression([NotNull] TSqlParser.Case_expressionContext context)
        {
            SwitchStatement result = null;
            if (context.caseExpr != null)
            {
                var caseExpr = (Expression)Visit(context.caseExpr);
                SwitchSection[] switchSection = context.switch_section()
                    .Select(ss => (SwitchSection)Visit(ss)).ToArray();
                result = new SwitchStatement(caseExpr, switchSection, context.GetTextSpan(), FileNode);
            }
            else
            {
                SwitchSection[] switchSection = context.switch_search_condition_section()
                    .Select(ss => (SwitchSection)Visit(ss)).ToArray();
                result = new SwitchStatement(null, switchSection, context.GetTextSpan(), FileNode);
            }
            return new WrapperExpression(result);
        }

        /// <returns><see cref="Expression"/></returns>
        public UstNode VisitBracket_expression([NotNull] TSqlParser.Bracket_expressionContext context)
        {
            return Visit(context.expression());
        }

        /// <returns><see cref="UnaryOperatorExpression"/></returns>
        public UstNode VisitUnary_operator_expression([NotNull] TSqlParser.Unary_operator_expressionContext context)
        {
            var expr = (Expression)Visit(context.expression());
            UnaryOperator op = UnaryOperatorLiteral.PrefixTextUnaryOperator[context.GetChild(0).GetText()];
            var opLiteral = new UnaryOperatorLiteral(op, context.GetTextSpan(), FileNode);
            var result = new UnaryOperatorExpression(opLiteral, expr, context.GetTextSpan(), FileNode);
            return result;
        }

        /// <returns><see cref="Expression"/></returns>
        public UstNode VisitConstant_expression([NotNull] TSqlParser.Constant_expressionContext context)
        {
            Expression result;
            if (context.NULL() != null)
            {
                result = new NullLiteral(context.GetTextSpan(), FileNode);
            }
            else if (context.constant() != null)
            {
                result = (Token)Visit(context.constant());
            }
            else if (context.function_call() != null)
            {
                result = (InvocationExpression)Visit(context.function_call());
            }
            else if (context.constant_expression() != null)
            {
                result = (Expression)Visit(context.constant_expression());
            }
            else
            {
                result = (Token)Visit(context.LOCAL_ID());
            }
            return result;
        }

        /// <returns><see cref="WrapperExpression"/></returns>
        public UstNode VisitSubquery([NotNull] TSqlParser.SubqueryContext context)
        {
            return new WrapperExpression((Statement)Visit(context.select_statement()),
                context.GetTextSpan(), FileNode);
        }

        /// <returns><see cref="InvocationExpression"/></returns>
        public UstNode VisitWith_expression([NotNull] TSqlParser.With_expressionContext context)
        {
            List<Expression> exprs = context.common_table_expression()
                .Select(expr => (Expression)Visit(expr)).ToList();
            var result = CreateSpecialInvocation(context.WITH(), context, exprs);
            return result;
        }

        /// <returns><see cref="MultichildExpression"/></returns>
        public UstNode VisitCommon_table_expression([NotNull] TSqlParser.Common_table_expressionContext context)
        {
            var exprs = new List<Expression>();
            if (context.column_name_list() != null)
            {
                exprs.AddRange(((ArgsNode)Visit(context.column_name_list())).Collection);
            }
            var selectStatement = (Statement)Visit(context.select_statement());
            exprs.Add(new WrapperExpression(selectStatement));

            var result = new MultichildExpression(exprs, context.GetTextSpan(), FileNode);
            return result;
        }

        /// <returns><see cref="MultichildExpression"/></returns>
        public UstNode VisitUpdate_elem([NotNull] TSqlParser.Update_elemContext context)
        {
            Expression[] children = context.children.Select(child => (Expression)Visit(child)).ToArray();
            var result = new MultichildExpression(children, FileNode);
            return result;
        }

        /// <returns><see cref="ArgsNode"/></returns>
        public UstNode VisitSearch_condition_list([NotNull] TSqlParser.Search_condition_listContext context)
        {
            var exprs =
                context.search_condition().Select(condition => (Expression)Visit(condition)).ToArray();
            var result = new ArgsNode(exprs);
            return result;
        }

        /// <returns><see cref="Expression"/></returns>
        public UstNode VisitSearch_condition([NotNull] TSqlParser.Search_conditionContext context)
        {
            Expression result = (Expression)Visit(context.search_condition_and(0));
            if (context.search_condition_and().Length > 1)
            {
                var firstSpan = context.search_condition_and(0).GetTextSpan();
                for (int i = 1; i < context.search_condition_and().Length; i++)
                {
                    var andOpLiteral = new BinaryOperatorLiteral(BinaryOperator.LogicalOr,
                        context.GetChild<ITerminalNode>(i - 1).GetTextSpan(), FileNode);
                    var rightExpression = (Expression)Visit(context.search_condition_and(i));
                    result = new BinaryOperatorExpression(result, andOpLiteral, rightExpression,
                        firstSpan.Union(rightExpression.TextSpan), FileNode);
                }
            }
            return result;
        }

        /// <returns><see cref="Expression"/></returns>
        public UstNode VisitSearch_condition_and([NotNull] TSqlParser.Search_condition_andContext context)
        {
            Expression result = (Expression)Visit(context.search_condition_not(0));
            if (context.search_condition_not().Length > 1)
            {
                var firstSpan = context.search_condition_not(0).GetTextSpan();
                for (int i = 1; i < context.search_condition_not().Length; i += 1)
                {
                    var andOpLiteral = new BinaryOperatorLiteral(BinaryOperator.LogicalAnd,
                        context.GetChild<ITerminalNode>(i - 1).GetTextSpan(), FileNode);
                    var rightExpression = (Expression)Visit(context.search_condition_not(i));
                    result = new BinaryOperatorExpression(result, andOpLiteral, rightExpression,
                        firstSpan.Union(rightExpression.TextSpan), FileNode);
                }
            }
            return result;
        }

        public UstNode VisitSearch_condition_not([NotNull] TSqlParser.Search_condition_notContext context)
        {
            var result = (Expression)Visit(context.predicate());
            if (context.NOT() != null)
            {
                var notOp = new UnaryOperatorLiteral(UnaryOperator.Not, context.NOT().GetTextSpan(), FileNode);
                result = new UnaryOperatorExpression(notOp, result, context.GetTextSpan(), FileNode);
            }
            return result;
        }

        /// <returns><see cref="Expression"/></returns>
        public UstNode VisitPredicate([NotNull] TSqlParser.PredicateContext context)
        {
            Expression result = null;
            var textSpan = context.GetTextSpan();
            if (context.EXISTS() != null)
            {
                var args = new ArgsNode((Expression)Visit(context.subquery()));
                result = new InvocationExpression(new IdToken(context.EXISTS().GetText()), args, textSpan, FileNode);
            }
            else if (context.search_condition() != null)
            {
                result = (Expression)Visit(context.search_condition());
            }
            else
            {
                if (context.comparison_operator() != null)
                {
                    Expression left = (Expression)Visit(context.expression(0));
                    Expression right;
                    if (context.expression().Length == 2)
                    {
                        right = (Expression)Visit(context.expression(1));
                    }
                    else
                    {
                        right = (Expression)Visit(context.subquery());
                    }
                    var opLiteral = (BinaryOperatorLiteral)Visit(context.comparison_operator());
                    result = new BinaryOperatorExpression(left, opLiteral, right, textSpan, FileNode);
                }
                else
                {
                    if (context.BETWEEN() != null)
                    {
                        // expr0 BETWEEN expr1 AND expr2 =>
                        // expr0 >= expr1 && expr0 <= expr2
                        var expr = (Expression)Visit(context.expression(0));
                        var greaterLiteral = new BinaryOperatorLiteral(BinaryOperator.GreaterOrEqual,
                            context.BETWEEN().GetTextSpan(), FileNode);
                        var lessLiteral = new BinaryOperatorLiteral(BinaryOperator.LessOrEqual,
                            context.AND().GetTextSpan(), FileNode);
                        var greaterExpr = new BinaryOperatorExpression(expr, greaterLiteral,
                            (Expression)Visit(context.expression(1)),
                            context.BETWEEN().GetTextSpan().Union(context.expression(1).GetTextSpan()), FileNode);
                        var lessExpr = new BinaryOperatorExpression(expr, lessLiteral,
                            (Expression)Visit(context.expression(2)),
                            context.AND().GetTextSpan().Union(context.expression(2).GetTextSpan()), FileNode);
                        var andLiteral = new BinaryOperatorLiteral(BinaryOperator.LogicalAnd,
                            context.AND().GetTextSpan(), FileNode);
                        result = new BinaryOperatorExpression(greaterExpr, andLiteral, lessExpr,
                            textSpan, FileNode);
                    }
                    else if (context.IN() != null)
                    {
                        var exprs = new List<Expression>();
                        if (context.subquery() != null)
                        {
                            exprs.Add((Expression)Visit(context.subquery()));
                        }
                        else
                        {
                            exprs.AddRange(GetArgsNode(context.expression_list()).Collection);
                        }
                        var eqLiteral = new BinaryOperatorLiteral(BinaryOperator.Equal,
                            context.IN().GetTextSpan(), FileNode);
                        var leftExpression = (Expression)Visit(context.expression(0));
                        result = new BinaryOperatorExpression(leftExpression, eqLiteral, exprs[0],
                            context.expression(0).GetTextSpan().Union(exprs[0].TextSpan), FileNode);
                        for (int i = 1; i < exprs.Count; i++)
                        {
                            var orLiteral = new BinaryOperatorLiteral(BinaryOperator.LogicalOr,
                                context.IN().GetTextSpan(), FileNode);
                            var rightExpr = new BinaryOperatorExpression(
                                leftExpression, eqLiteral, exprs[i], exprs[i].TextSpan, FileNode);
                            result = new BinaryOperatorExpression(result, orLiteral, rightExpr,
                                leftExpression.TextSpan.Union(exprs[i].TextSpan), FileNode);
                        }
                    }
                    else if (context.LIKE() != null)
                    {
                        var left = (Expression)Visit(context.expression(0));
                        var right = (Expression)Visit(context.expression(1));
                        var equalLiteral = new BinaryOperatorLiteral(BinaryOperator.Equal, context.LIKE().GetTextSpan(), FileNode);
                        result = new BinaryOperatorExpression(left, equalLiteral, right, textSpan, FileNode);
                    }
                    else // IS
                    {
                        var functionName = (IdToken)Visit(context.IS());
                        var args = new List<Expression>();
                        args.Add((Expression)Visit(context.expression(0)));
                        args.Add((Expression)Visit(context.null_notnull()));
                        result = new InvocationExpression(functionName, new ArgsNode(args), textSpan, FileNode);
                    }
                    if (context.NOT() != null)
                    {
                        var notLiteral = new UnaryOperatorLiteral(UnaryOperator.Not, context.NOT().GetTextSpan(), FileNode);
                        result = new UnaryOperatorExpression(notLiteral, result, textSpan, FileNode);
                    }
                }
            }
            return result;
        }

        /// <returns><see cref="Expression"/></returns>
        public UstNode VisitQuery_expression([NotNull] TSqlParser.Query_expressionContext context)
        {
            Expression result;

            if (context.query_specification() != null)
            {
                result = (Expression)Visit(context.query_specification());
            }
            else
            {
                result = (Expression)Visit(context.query_expression());
            }

            if (context.union().Length > 0)
            {
                result = new MultichildExpression(new List<Expression>(
                    context.union().Select(union => (InvocationExpression)Visit(union))), FileNode);
            }
            return result;
        }

        /// <returns><see cref="InvocationExpression"/></returns>
        public UstNode VisitUnion([NotNull] TSqlParser.UnionContext context)
        {
            var exprs = new List<Expression>();
            if (context.query_specification() != null)
            {
                exprs.Add((MultichildExpression)Visit(context.query_specification()));
            }
            else
            {
                exprs.Add((Expression)Visit(context.query_expression()));
            }

            InvocationExpression result = CreateSpecialInvocation(context.GetChild<ITerminalNode>(0), context, exprs);
            return result;
        }

        /// <returns><see cref="MultichildExpression"/></returns>
        public UstNode VisitQuery_specification([NotNull] TSqlParser.Query_specificationContext context)
        {
            return VisitChildren(context);
        }

        /// <returns><see cref="InvocationExpression"/></returns>
        public UstNode VisitOrder_by_clause([NotNull] TSqlParser.Order_by_clauseContext context)
        {
            var idToken = new IdToken(context.ORDER().GetText() + context.BY().GetText(),
                context.ORDER().GetTextSpan(), FileNode);
            List<Expression> exprs = context.order_by_expression()
                .Select(expr => (Expression)Visit(expr)).ToList();
            exprs.AddRange(context.expression().Select(expr => (Expression)Visit(expr)));
            var result = new InvocationExpression(idToken, new ArgsNode(exprs), context.GetTextSpan(), FileNode);
            return result;
        }

        /// <returns><see cref="InvocationExpression"/></returns>
        public UstNode VisitFor_clause([NotNull] TSqlParser.For_clauseContext context)
        {
            var exprs = new List<Expression>();
            if (context.STRING() != null)
            {
                exprs.Add((Token)Visit(context.STRING()));
            }
            InvocationExpression result = CreateSpecialInvocation(context.FOR(), context, exprs);
            return result;
        }

        /// <returns><see cref="IdToken"/></returns>
        public UstNode VisitXml_common_directives([NotNull] TSqlParser.Xml_common_directivesContext context)
        {
            return (IdToken)Visit(context.GetChild(1));
        }

        /// <returns><see cref="Expression"/></returns>
        public UstNode VisitOrder_by_expression([NotNull] TSqlParser.Order_by_expressionContext context)
        {
            return Visit(context.expression());
        }

        /// <returns><see cref="Expression"/></returns>
        public UstNode VisitGroup_by_item([NotNull] TSqlParser.Group_by_itemContext context)
        {
            return Visit(context.expression());
        }

        /// <returns><see cref="InvocationExpression"/></returns>
        public UstNode VisitOption_clause([NotNull] TSqlParser.Option_clauseContext context)
        {
            List<Expression> exprs = context.option()
                .Select(o => (Expression)Visit(o)).ToList();
            var result = CreateSpecialInvocation(context.OPTION(), context, exprs);
            return result;
        }

        /// <returns><see cref="InvocationExpression"/></returns>
        public UstNode VisitOption([NotNull] TSqlParser.OptionContext context)
        {
            var exprs = new List<Expression>();
            IdToken functionName;
            if (context.optimize_for_arg().Length > 0)
            {
                exprs.AddRange(context.optimize_for_arg().Select(arg => (AssignmentExpression)Visit(arg))
                    .ToArray());
                functionName = new IdToken(context.OPTIMIZE().GetText() + context.FOR().GetText(),
                    context.OPTIMIZE().GetTextSpan().Union(context.FOR().GetTextSpan()), FileNode);
            }
            else
            {
                functionName = new IdToken(context.GetText(), context.GetTextSpan(), FileNode);
            }
            var result = new InvocationExpression(functionName, new ArgsNode(exprs), context.GetTextSpan(), FileNode);
            return result;
        }

        /// <returns><see cref="AssignmentExpression"/></returns>
        public UstNode VisitOptimize_for_arg([NotNull] TSqlParser.Optimize_for_argContext context)
        {
            Expression right;
            if (context.UNKNOWN() != null)
            {
                right = (Token)Visit(context.LOCAL_ID());
            }
            else
            {
                if (context.constant() != null)
                {
                    right = (Token)Visit(context.constant());
                }
                else
                {
                    right = (Token)Visit(context.NULL());
                }
            }
            var result = new AssignmentExpression((Token)Visit(context.LOCAL_ID()), right, context.GetTextSpan(), FileNode);
            return result;
        }

        /// <returns><see cref="Expression"/></returns>
        public UstNode VisitSelect_list([NotNull] TSqlParser.Select_listContext context)
        {
            var result = new MultichildExpression(context.select_list_elem().Select(
                elem => (Expression)Visit(elem)).ToList(), context.GetTextSpan(), FileNode);
            return result;
        }

        /// <returns><see cref="Expression"/></returns>
        public UstNode VisitSelect_list_elem([NotNull] TSqlParser.Select_list_elemContext context)
        {
            return Visit(context.GetChild(0));
        }

        /// <returns><see cref="MultichildExpression"/></returns>
        public UstNode VisitTable_sources([NotNull] TSqlParser.Table_sourcesContext context)
        {
            var result = new MultichildExpression(
                context.table_source().Select(tableSource =>
                (Expression)Visit(tableSource)).ToList(), FileNode);
            return result;
        }

        /// <returns><see cref="MultichildExpression"/></returns>
        public UstNode VisitTable_source([NotNull] TSqlParser.Table_sourceContext context)
        {
            return (MultichildExpression)Visit(context.table_source_item_joined());
        }

        /// <returns><see cref="MultichildExpression"/></returns>
        public UstNode VisitTable_source_item_joined([NotNull] TSqlParser.Table_source_item_joinedContext context)
        {
            return VisitChildren(context);
        }

        /// <returns><see cref="Expression"/></returns>
        public UstNode VisitTable_source_item([NotNull] TSqlParser.Table_source_itemContext context)
        {
            var exprs = new List<Expression>();
            if (context.as_table_alias() != null)
            {
                exprs.Add((Expression)Visit(context.as_table_alias()));
            }
            if (context.column_alias_list() != null)
            {
                exprs.Add((Expression)Visit(context.column_alias_list()));
            }

            if (context.LOCAL_ID() == null)
            {
                if (context.function_call() != null)
                {
                    exprs.Add((Expression)Visit(context.function_call()));
                }
                else
                {
                    exprs.Add((Expression)Visit(context.GetChild(0)));
                }
            }
            else
            {
                Token expr = (Token)Visit(context.LOCAL_ID());
                if (context.function_call() != null)
                {
                    exprs.Add(new MemberReferenceExpression(expr,
                        (InvocationExpression)Visit(context.function_call()),
                        context.LOCAL_ID().GetTextSpan().Union(context.function_call().GetTextSpan()), FileNode));
                }
            }

            return new MultichildExpression(exprs, FileNode);
        }

        /// <returns><see cref="InvocationExpression"/></returns>
        public UstNode VisitJoin_part([NotNull] TSqlParser.Join_partContext context)
        {
            return VisitChildren(context);
        }

        /// <returns><see cref="Expression"/></returns>
        public UstNode VisitTable_name_with_hint([NotNull] TSqlParser.Table_name_with_hintContext context)
        {
            Expression result = (IdToken)Visit(context.table_name());
            if (context.with_table_hints() != null)
            {
                var multichild = (MultichildExpression)Visit(context.with_table_hints());
                multichild.Expressions.Add(result);
                result = multichild;
            }
            return result;
        }

        /// <returns><see cref="InvocationExpression"/></returns>
        public UstNode VisitRowset_function([NotNull] TSqlParser.Rowset_functionContext context)
        {
            return CreateSpecialInvocation(context.OPENROWSET(), context,
                context.bulk_option().Select(opt => (Expression)Visit(opt)).ToList());
        }

        /// <returns><see cref="AssignmentExpression"/></returns>
        public UstNode VisitBulk_option([NotNull] TSqlParser.Bulk_optionContext context)
        {
            var left = (IdToken)Visit(context.id());
            var right = ExtractLiteral(context.bulk_option_value);
            var result = new AssignmentExpression(left, right, context.GetTextSpan(), FileNode);
            return result;
        }

        /// <returns><see cref="Expression"/></returns>
        public UstNode VisitDerived_table([NotNull] TSqlParser.Derived_tableContext context)
        {
            return VisitChildren(context);
        }

        /// <returns><see cref="InvocationExpression"/></returns>
        public UstNode VisitChange_table([NotNull] TSqlParser.Change_tableContext context)
        {
            var tableName = (IdToken)Visit(context.table_name());
            var result = CreateSpecialInvocation(context.CHANGETABLE(), context, tableName);
            return result;
        }

        /// <returns><see cref="InvocationExpression"/></returns>
        public UstNode VisitFunction_call([NotNull] TSqlParser.Function_callContext context)
        {
            InvocationExpression result = null;
            if (context.ranking_windowed_function() != null)
            {
                result = (InvocationExpression)Visit(context.ranking_windowed_function());
            }
            else if (context.aggregate_windowed_function() != null)
            {
                result = (InvocationExpression)Visit(context.aggregate_windowed_function());
            }
            else if (context.scalar_function_name() != null)
            {
                var target = (Expression)Visit(context.scalar_function_name());
                var args = GetArgsNode(context.expression_list());
                result = new InvocationExpression(target, args, context.GetTextSpan(), FileNode);
            }
            else if (context.xml_data_type_methods() != null)
            {
                result = (InvocationExpression)Visit(context.xml_data_type_methods());
            }
            else
            {
                var exprs = new List<Expression>();
                exprs.AddRange(context.expression().Select(expr => (Expression)Visit(expr)));
                if (context.expression_list() != null)
                {
                    exprs.AddRange(GetArgsNode(context.expression_list()).Collection);
                }
                if (context.data_type() != null)
                {
                    exprs.Add((TypeToken)Visit(context.data_type()));
                }
                if (context.seed != null)
                {
                    exprs.Add(ExtractLiteral(context.seed));
                }
                if (context.increment != null)
                {
                    exprs.Add(ExtractLiteral(context.increment));
                }
                result = CreateSpecialInvocation(context.GetChild<ITerminalNode>(0), context, exprs);
            }
            return result;
        }

        /// <returns><see cref="SwitchSection"/></returns>
        public UstNode VisitSwitch_section([NotNull] TSqlParser.Switch_sectionContext context)
        {
            var caseLabels = new Expression[] { (Expression)Visit(context.expression(0)) };
            var statements = new Statement[] { new ExpressionStatement(
                (Expression)Visit(context.expression(1))) };
            var result = new SwitchSection(caseLabels, statements, context.GetTextSpan(), FileNode);
            return result;
        }

        /// <returns><see cref="SwitchSection"/></returns>
        public UstNode VisitSwitch_search_condition_section([NotNull] TSqlParser.Switch_search_condition_sectionContext context)
        {
            var caseLabels = new Expression[] { (Expression)Visit(context.search_condition()) };
            var statements = new Statement[] { new ExpressionStatement(
                (Expression)Visit(context.expression())) };
            var result = new SwitchSection(caseLabels, statements, context.GetTextSpan(), FileNode);
            return result;
        }

        /// <returns><see cref="Expression"/></returns>
        public UstNode VisitAs_table_alias([NotNull] TSqlParser.As_table_aliasContext context)
        {
            return (Expression)Visit(context.table_alias());
        }

        /// <returns><see cref="Expression"/></returns>
        public UstNode VisitTable_alias([NotNull] TSqlParser.Table_aliasContext context)
        {
            Expression result = (IdToken)Visit(context.id());
            if (context.with_table_hints() != null)
            {
                var multichild = (MultichildExpression)Visit(context.with_table_hints());
                multichild.Expressions.Add(result);
                result = multichild;
            }
            return result;
        }

        /// <returns><see cref="MultichildExpression"/></returns>
        public UstNode VisitWith_table_hints([NotNull] TSqlParser.With_table_hintsContext context)
        {
            var exprs = context.table_hint().Select(hint => (Expression)Visit(hint)).ToList();
            var result = new MultichildExpression(exprs, context.GetTextSpan(), FileNode);
            return result;
        }

        /// <returns><see cref="MultichildExpression"/></returns>
        public UstNode VisitTable_hint([NotNull] TSqlParser.Table_hintContext context)
        {
            return VisitChildren(context);
        }

        /// <returns><see cref="Token"/></returns>
        public UstNode VisitIndex_value([NotNull] TSqlParser.Index_valueContext context)
        {
            return VisitChildren(context);
        }

        /// <returns><see cref="MultichildExpression"/></returns>
        public UstNode VisitColumn_alias_list([NotNull] TSqlParser.Column_alias_listContext context)
        {
            var exprs = context.column_alias().Select(column_alias => (Token)Visit(column_alias)).ToArray();
            return new MultichildExpression(exprs, context.GetTextSpan(), FileNode);
        }

        /// <returns><see cref="Token"/></returns>
        public UstNode VisitColumn_alias([NotNull] TSqlParser.Column_aliasContext context)
        {
            Token result;
            if (context.id() != null)
            {
                result = (Token)Visit(context.id());
            }
            else
            {
                result = (Token)Visit(context.STRING());
            }
            return result;
        }

        /// <returns><see cref="InvocationExpression"/></returns>
        public UstNode VisitTable_value_constructor([NotNull] TSqlParser.Table_value_constructorContext context)
        {
            var args1 = GetArgsNode(context.expression_list(0));
            var args2 = GetArgsNode(context.expression_list(1));
            var newArgs = new ArgsNode(new WrapperExpression(args1), new WrapperExpression(args2));

            var name = new IdToken(context.VALUES().GetText().ToLowerInvariant(), context.VALUES().GetTextSpan(), FileNode);
            var result = new InvocationExpression(name, newArgs, context.GetTextSpan(), FileNode);
            return result;
        }

        public UstNode VisitExpression_list([NotNull] TSqlParser.Expression_listContext context)
        {
            return new MultichildExpression(context.expression().Select(expr => (Expression)Visit(expr)), FileNode);
        }

        /// <returns><see cref="InvocationExpression"/></returns>
        public UstNode VisitRanking_windowed_function([NotNull] TSqlParser.Ranking_windowed_functionContext context)
        {
            var exprs = new List<Expression>();
            if (context.expression() != null)
            {
                exprs.Add((Expression)Visit(context.expression()));
            }
            exprs.Add((Expression)Visit(context.over_clause()));

            InvocationExpression result = CreateSpecialInvocation(context.GetChild<ITerminalNode>(0), context, exprs);
            return result;
        }

        /// <returns><see cref="InvocationExpression"/></returns>
        public UstNode VisitAggregate_windowed_function([NotNull] TSqlParser.Aggregate_windowed_functionContext context)
        {
            var terminal = context.GetChild<ITerminalNode>(0);
            var functionName = new IdToken(terminal.GetText(), terminal.GetTextSpan(), FileNode);

            var exprs = new List<Expression>();
            if (context.all_distinct_expression() != null)
            {
                exprs.Add((Expression)Visit(context.all_distinct_expression()));
            }
            if (context.over_clause() != null)
            {
                exprs.Add((Expression)Visit(context.over_clause()));
            }
            if (context.expression() != null)
            {
                exprs.Add((Expression)Visit(context.expression()));
            }
            if (context.expression_list() != null)
            {
                exprs.Add(new WrapperExpression(GetArgsNode(context.expression_list())));
            }

            InvocationExpression result = CreateSpecialInvocation(context.GetChild<ITerminalNode>(0), context, exprs);
            return result;
        }

        /// <returns><see cref="Expression"/></returns>
        public UstNode VisitAll_distinct_expression([NotNull] TSqlParser.All_distinct_expressionContext context)
        {
            return Visit(context.expression());
        }

        /// <returns><see cref="InvocationExpression"/></returns>
        public UstNode VisitOver_clause([NotNull] TSqlParser.Over_clauseContext context)
        {
            var exprs = new List<Expression>();
            if (context.expression_list() != null)
            {
                context.expression_list().expression().Select(expr => (Expression)Visit(expr)).ToList();
            }

            if (context.order_by_clause() != null)
            {
                exprs.Add((Expression)Visit(context.order_by_clause()));
            }

            string rowOrRangeClause;
            if (context.row_or_range_clause() != null)
            {
                rowOrRangeClause = context.row_or_range_clause().GetText();
                exprs.Add(new IdToken(rowOrRangeClause, context.row_or_range_clause().GetTextSpan(), FileNode));
            }

            var functionName = new IdToken(context.OVER().GetText(), context.OVER().GetTextSpan(), FileNode);
            var result = new InvocationExpression(functionName, new ArgsNode(exprs), context.GetTextSpan(), FileNode);
            return result;
        }

        /// <returns><see cref="Token"/></returns>
        public UstNode VisitRow_or_range_clause([NotNull] TSqlParser.Row_or_range_clauseContext context)
        {
            return VisitShouldNotBeVisited(context);
        }

        public UstNode VisitWindow_frame_extent([NotNull] TSqlParser.Window_frame_extentContext context)
        {
            return VisitShouldNotBeVisited(context);
        }

        public UstNode VisitWindow_frame_bound([NotNull] TSqlParser.Window_frame_boundContext context)
        {
            return VisitShouldNotBeVisited(context);
        }

        public UstNode VisitWindow_frame_preceding([NotNull] TSqlParser.Window_frame_precedingContext context)
        {
            return VisitShouldNotBeVisited(context);
        }

        public UstNode VisitWindow_frame_following([NotNull] TSqlParser.Window_frame_followingContext context)
        {
            return VisitShouldNotBeVisited(context);
        }

        #endregion

        #region Primitive

        /// <returns><see cref="IdToken"/></returns>
        public UstNode VisitFull_table_name([NotNull] TSqlParser.Full_table_nameContext context)
        {
            return new IdToken(RemoveSpaces(context.GetText()), context.GetTextSpan(), FileNode);
        }

        /// <returns><see cref="IdToken"/></returns>
        public UstNode VisitTable_name([NotNull] TSqlParser.Table_nameContext context)
        {
            // (database=id '.' (schema=id)? '.' | schema=id '.')? table=id
            return new IdToken(RemoveSpaces(context.GetText()), context.GetTextSpan(), FileNode);
        }

        /// <returns><see cref="IdToken"/></returns>
        public UstNode VisitSimple_name([NotNull] TSqlParser.Simple_nameContext context)
        {
            return new IdToken(RemoveSpaces(context.GetText()), context.GetTextSpan(), FileNode);
        }

        /// <returns><see cref="IdToken"/></returns>
        public UstNode VisitFunc_proc_name([NotNull] TSqlParser.Func_proc_nameContext context)
        {
            return new IdToken(RemoveSpaces(context.GetText()), context.GetTextSpan(), FileNode);
        }

        /// <returns><see cref="Token"/></returns>
        public UstNode VisitDdl_object([NotNull] TSqlParser.Ddl_objectContext context)
        {
            Token result;
            if (context.full_table_name() != null)
            {
                result = (Token)Visit(context.full_table_name());
            }
            else
            {
                result = (Token)Visit(context.LOCAL_ID());
            }
            return result;
        }

        /// <returns><see cref="Expression"/></returns>
        public UstNode VisitFull_column_name([NotNull] TSqlParser.Full_column_nameContext context)
        {
            if (context.table_name() != null)
            {
                return new MemberReferenceExpression(
                    (Expression)Visit(context.table_name()),
                    (Expression)Visit(context.id()), context.GetTextSpan(), FileNode);
            }
            else
            {
                return Visit(context.id());
            }
        }

        /// <returns><see cref="ArgsNode"/></returns>
        public UstNode VisitColumn_name_list([NotNull] TSqlParser.Column_name_listContext context)
        {
            var result = new ArgsNode(context.id().Select(id => (IdToken)Visit(id)).ToArray());
            return result;
        }

        /// <returns><see cref="Token"/></returns>
        public UstNode VisitCursor_name([NotNull] TSqlParser.Cursor_nameContext context)
        {
            Token result;
            if (context.id() != null)
            {
                result = (IdToken)Visit(context.id());
            }
            else
            {
                result = (Token)Visit(context.LOCAL_ID());
            }
            return result;
        }

        /// <returns><see cref="BooleanLiteral"/></returns>
        public UstNode VisitOn_off([NotNull] TSqlParser.On_offContext context)
        {
            var text = context.GetText().ToLowerInvariant();
            return new BooleanLiteral(text == "on", context.GetTextSpan(), FileNode);
        }

        /// <returns><see cref="IdToken"/></returns>
        public UstNode VisitClustered([NotNull] TSqlParser.ClusteredContext context)
        {
            var result = new IdToken(context.GetText(), context.GetTextSpan(), FileNode);
            return result;
        }

        /// <returns><see cref="Expression"/></returns>
        public UstNode VisitNull_notnull([NotNull] TSqlParser.Null_notnullContext context)
        {
            Expression result = new NullLiteral(context.NULL().GetTextSpan(), FileNode);
            if (context.NOT() != null)
            {
                var literal = new UnaryOperatorLiteral(UnaryOperator.Not, context.GetTextSpan(), FileNode);
                result = new UnaryOperatorExpression(literal, result, context.GetTextSpan(), FileNode);
            }
            return result;
        }

        /// <returns><see cref="Expression"/></returns>
        public UstNode VisitScalar_function_name([NotNull] TSqlParser.Scalar_function_nameContext context)
        {
            if (context.func_proc_name() != null)
            {
                return Visit(context.func_proc_name());
            }
            else
            {
                return new IdToken(context.GetText(), context.GetTextSpan(), FileNode);
            }
        }

        /// <returns><see cref="TypeToken"/></returns>
        public UstNode VisitData_type([NotNull] TSqlParser.Data_typeContext context)
        {
            var resultType = RemoveSpaces(context.GetText());
            return new TypeToken(resultType, context.GetTextSpan(), FileNode);
        }

        /// <returns><see cref="Token"/></returns>
        public UstNode VisitDefault_value([NotNull] TSqlParser.Default_valueContext context)
        {
            Token result;
            if (context.NULL() != null)
            {
                result = new NullLiteral(context.GetTextSpan(), FileNode);
            }
            else
            {
                result = (Token)Visit(context.constant());
            }
            return result;
        }

        /// <returns><see cref="Token"/></returns>
        public UstNode VisitConstant([NotNull] TSqlParser.ConstantContext context)
        {
            var text = context.GetText();
            var textSpan = context.GetTextSpan();
            Token result;
            if (context.STRING() != null)
            {
                if (text.StartsWith("N"))
                {
                    text = text.Substring(1);
                }
                result = new StringLiteral(text.Substring(1, text.Length - 2), textSpan, FileNode);
            }
            else if (context.BINARY() != null)
            {
                result = new IntLiteral(Convert.ToInt64(text.Substring(2), 16), textSpan, FileNode);
            }
            else if (context.dollar != null)
            {
                result = new StringLiteral(text, textSpan, FileNode);
            }
            else if (context.DECIMAL() != null)
            {
                result = new IntLiteral(long.Parse(text), textSpan, FileNode);
            }
            else
            {
                result = new FloatLiteral(double.Parse(text), textSpan, FileNode);
            }
            return result;
        }

        public UstNode VisitSign([NotNull] TSqlParser.SignContext context)
        {
            return VisitShouldNotBeVisited(context);
        }

        /// <returns><see cref="IdToken"/></returns>
        public UstNode VisitId([NotNull] TSqlParser.IdContext context)
        {
            string id;
            if (context.simple_id() != null)
            {
                id = context.simple_id().GetText();
            }
            else
            {
                id = context.GetText();
                id = id.Substring(1, id.Length - 2);
            }
            return new IdToken(id, context.GetTextSpan(), FileNode);
        }

        /// <returns><see cref="IdToken"/></returns>
        public UstNode VisitSimple_id([NotNull] TSqlParser.Simple_idContext context)
        {
            return new IdToken(context.GetText(), context.GetTextSpan(), FileNode);
        }

        /// <returns><see cref="BinaryOperatorLiteral"/></returns>
        public UstNode VisitComparison_operator([NotNull] TSqlParser.Comparison_operatorContext context)
        {
            var opText = RemoveSpaces(context.GetText());
            if (opText == "=")
            {
                opText = "==";
            }
            else if (opText == "<>")
            {
                opText = "!=";
            }
            var result = new BinaryOperatorLiteral(opText, context.GetTextSpan(), FileNode);
            return result;
        }

        /// <returns><see cref="BinaryOperatorLiteral"/></returns>
        public UstNode VisitAssignment_operator([NotNull] TSqlParser.Assignment_operatorContext context)
        {
            var result = new BinaryOperatorLiteral(RemoveSpaces(context.GetText()), context.GetTextSpan(), FileNode);
            return result;
        }

        #endregion

        #region Without implementation

        public UstNode VisitCreate_database([NotNull] TSqlParser.Create_databaseContext context)
        {
            return VisitChildren(context);
        }

        public UstNode VisitAuto_option([NotNull] TSqlParser.Auto_optionContext context)
        {
            return VisitChildren(context);
        }

        public UstNode VisitChange_tracking_option([NotNull] TSqlParser.Change_tracking_optionContext context)
        {
            return VisitChildren(context);
        }

        public UstNode VisitChange_tracking_option_list([NotNull] TSqlParser.Change_tracking_option_listContext context)
        {
            return VisitChildren(context);
        }

        public UstNode VisitContainment_option([NotNull] TSqlParser.Containment_optionContext context)
        {
            return VisitChildren(context);
        }

        public UstNode VisitCursor_option([NotNull] TSqlParser.Cursor_optionContext context)
        {
            return VisitChildren(context);
        }

        public UstNode VisitDate_correlation_optimization_option([NotNull] TSqlParser.Date_correlation_optimization_optionContext context)
        {
            return VisitChildren(context);
        }

        public UstNode VisitDb_encryption_option([NotNull] TSqlParser.Db_encryption_optionContext context)
        {
            return VisitChildren(context);
        }

        public UstNode VisitDb_state_option([NotNull] TSqlParser.Db_state_optionContext context)
        {
            return VisitChildren(context);
        }

        public UstNode VisitDb_update_option([NotNull] TSqlParser.Db_update_optionContext context)
        {
            return VisitChildren(context);
        }

        public UstNode VisitDb_user_access_option([NotNull] TSqlParser.Db_user_access_optionContext context)
        {
            return VisitChildren(context);
        }

        public UstNode VisitDelayed_durability_option([NotNull] TSqlParser.Delayed_durability_optionContext context)
        {
            return VisitChildren(context);
        }

        public UstNode VisitExternal_access_option([NotNull] TSqlParser.External_access_optionContext context)
        {
            return VisitChildren(context);
        }

        public UstNode VisitMixed_page_allocation_option([NotNull] TSqlParser.Mixed_page_allocation_optionContext context)
        {
            return VisitChildren(context);
        }

        public UstNode VisitParameterization_option([NotNull] TSqlParser.Parameterization_optionContext context)
        {
            return VisitChildren(context);
        }

        public UstNode VisitRecovery_option([NotNull] TSqlParser.Recovery_optionContext context)
        {
            return VisitChildren(context);
        }

        public UstNode VisitService_broker_option([NotNull] TSqlParser.Service_broker_optionContext context)
        {
            return VisitChildren(context);
        }

        public UstNode VisitSnapshot_option([NotNull] TSqlParser.Snapshot_optionContext context)
        {
            return VisitChildren(context);
        }

        public UstNode VisitSql_option([NotNull] TSqlParser.Sql_optionContext context)
        {
            return VisitChildren(context);
        }

        public UstNode VisitTarget_recovery_time_option([NotNull] TSqlParser.Target_recovery_time_optionContext context)
        {
            return VisitChildren(context);
        }

        public UstNode VisitTermination([NotNull] TSqlParser.TerminationContext context)
        {
            return VisitChildren(context);
        }

        public UstNode VisitCreate_type([NotNull] TSqlParser.Create_typeContext context)
        {
            return VisitChildren(context);
        }

        public UstNode VisitDrop_type([NotNull] TSqlParser.Drop_typeContext context)
        {
            return VisitChildren(context);
        }

        public UstNode VisitCreate_database_option([NotNull] TSqlParser.Create_database_optionContext context)
        {
            return VisitChildren(context);
        }

        public UstNode VisitDatabase_filestream_option([NotNull] TSqlParser.Database_filestream_optionContext context)
        {
            return VisitChildren(context);
        }

        public UstNode VisitDatabase_file_spec([NotNull] TSqlParser.Database_file_specContext context)
        {
            return VisitChildren(context);
        }

        public UstNode VisitFile_group([NotNull] TSqlParser.File_groupContext context)
        {
            return VisitChildren(context);
        }

        public UstNode VisitFile_spec([NotNull] TSqlParser.File_specContext context)
        {
            return VisitChildren(context);
        }

        public UstNode VisitFile_size([NotNull] TSqlParser.File_sizeContext context)
        {
            return VisitChildren(context);
        }

        public UstNode VisitEmpty_statement([NotNull] TSqlParser.Empty_statementContext context)
        {
            return new EmptyStatement(context.GetTextSpan(), FileNode);
        }

        public UstNode VisitCreate_or_alter_function([NotNull] TSqlParser.Create_or_alter_functionContext context)
        {
            return VisitChildren(context);
        }

        public UstNode VisitFunc_body_returns_select([NotNull] TSqlParser.Func_body_returns_selectContext context)
        {
            return VisitChildren(context);
        }

        public UstNode VisitFunc_body_returns_table([NotNull] TSqlParser.Func_body_returns_tableContext context)
        {
            return VisitChildren(context);
        }

        public UstNode VisitFunc_body_returns_scalar([NotNull] TSqlParser.Func_body_returns_scalarContext context)
        {
            return VisitChildren(context);
        }

        public UstNode VisitFunction_option([NotNull] TSqlParser.Function_optionContext context)
        {
            return VisitChildren(context);
        }

        public UstNode VisitDrop_function([NotNull] TSqlParser.Drop_functionContext context)
        {
            return VisitChildren(context);
        }

        public UstNode VisitDbcc_clause([NotNull] TSqlParser.Dbcc_clauseContext context)
        {
            return VisitChildren(context);
        }

        public UstNode VisitDbcc_options([NotNull] TSqlParser.Dbcc_optionsContext context)
        {
            return VisitChildren(context);
        }

        public UstNode VisitTop_clause([NotNull] TSqlParser.Top_clauseContext context)
        {
            return VisitChildren(context);
        }

        public UstNode VisitTop_percent([NotNull] TSqlParser.Top_percentContext context)
        {
            return VisitChildren(context);
        }

        public UstNode VisitTop_count([NotNull] TSqlParser.Top_countContext context)
        {
            return VisitChildren(context);
        }

        #endregion

        public UstNode VisitCreate_queue([NotNull] TSqlParser.Create_queueContext context)
        {
            return VisitChildren(context);
        }

        public UstNode VisitQueue_settings([NotNull] TSqlParser.Queue_settingsContext context)
        {
            return VisitChildren(context);
        }

        public UstNode VisitAlter_queue([NotNull] TSqlParser.Alter_queueContext context)
        {
            return VisitChildren(context);
        }

        public UstNode VisitQueue_action([NotNull] TSqlParser.Queue_actionContext context)
        {
            return VisitChildren(context);
        }

        public UstNode VisitQueue_rebuild_options([NotNull] TSqlParser.Queue_rebuild_optionsContext context)
        {
            return VisitChildren(context);
        }

        public UstNode VisitCreate_contract([NotNull] TSqlParser.Create_contractContext context)
        {
            return VisitChildren(context);
        }

        public UstNode VisitConversation_statement([NotNull] TSqlParser.Conversation_statementContext context)
        {
            return VisitChildren(context);
        }

        public UstNode VisitMessage_statement([NotNull] TSqlParser.Message_statementContext context)
        {
            return VisitChildren(context);
        }

        public UstNode VisitMerge_statement([NotNull] TSqlParser.Merge_statementContext context)
        {
            return VisitChildren(context);
        }

        public UstNode VisitMerge_matched([NotNull] TSqlParser.Merge_matchedContext context)
        {
            return VisitChildren(context);
        }

        public UstNode VisitMerge_not_matched([NotNull] TSqlParser.Merge_not_matchedContext context)
        {
            return VisitChildren(context);
        }

        public UstNode VisitReceive_statement([NotNull] TSqlParser.Receive_statementContext context)
        {
            return VisitChildren(context);
        }

        public UstNode VisitTime([NotNull] TSqlParser.TimeContext context)
        {
            return VisitChildren(context);
        }

        public UstNode VisitCreate_or_alter_trigger([NotNull] TSqlParser.Create_or_alter_triggerContext context)
        {
            return VisitChildren(context);
        }

        public UstNode VisitDml_trigger([NotNull] TSqlParser.Dml_triggerContext context)
        {
            return VisitChildren(context);
        }

        public UstNode VisitDml_trigger_option([NotNull] TSqlParser.Dml_trigger_optionContext context)
        {
            return VisitChildren(context);
        }

        public UstNode VisitDml_trigger_operation([NotNull] TSqlParser.Dml_trigger_operationContext context)
        {
            return VisitChildren(context);
        }

        public UstNode VisitDdl_trigger([NotNull] TSqlParser.Ddl_triggerContext context)
        {
            return VisitChildren(context);
        }

        public UstNode VisitDdl_trigger_operation([NotNull] TSqlParser.Ddl_trigger_operationContext context)
        {
            return VisitChildren(context);
        }

        public UstNode VisitTable_options([NotNull] TSqlParser.Table_optionsContext context)
        {
            return VisitChildren(context);
        }

        public UstNode VisitTable_option([NotNull] TSqlParser.Table_optionContext context)
        {
            return VisitChildren(context);
        }

        public UstNode VisitHadr_options([NotNull] TSqlParser.Hadr_optionsContext context)
        {
            return VisitChildren(context);
        }

        public UstNode VisitDrop_trigger([NotNull] TSqlParser.Drop_triggerContext context)
        {
            return VisitChildren(context);
        }

        public UstNode VisitDrop_dml_trigger([NotNull] TSqlParser.Drop_dml_triggerContext context)
        {
            return VisitChildren(context);
        }

        public UstNode VisitDrop_ddl_trigger([NotNull] TSqlParser.Drop_ddl_triggerContext context)
        {
            return VisitChildren(context);
        }

        public UstNode VisitCreate_certificate([NotNull] TSqlParser.Create_certificateContext context)
        {
            return VisitChildren(context);
        }

        public UstNode VisitExisting_keys([NotNull] TSqlParser.Existing_keysContext context)
        {
            return VisitChildren(context);
        }

        public UstNode VisitPrivate_key_options([NotNull] TSqlParser.Private_key_optionsContext context)
        {
            return VisitChildren(context);
        }

        public UstNode VisitGenerate_new_keys([NotNull] TSqlParser.Generate_new_keysContext context)
        {
            return VisitChildren(context);
        }

        public UstNode VisitDate_options([NotNull] TSqlParser.Date_optionsContext context)
        {
            return VisitChildren(context);
        }

        public UstNode VisitOpen_key([NotNull] TSqlParser.Open_keyContext context)
        {
            return VisitChildren(context);
        }

        public UstNode VisitClose_key([NotNull] TSqlParser.Close_keyContext context)
        {
            return VisitChildren(context);
        }

        public UstNode VisitCreate_key([NotNull] TSqlParser.Create_keyContext context)
        {
            return VisitChildren(context);
        }

        public UstNode VisitKey_options([NotNull] TSqlParser.Key_optionsContext context)
        {
            return VisitChildren(context);
        }

        public UstNode VisitAlgorithm([NotNull] TSqlParser.AlgorithmContext context)
        {
            return VisitChildren(context);
        }

        public UstNode VisitEncryption_mechanism([NotNull] TSqlParser.Encryption_mechanismContext context)
        {
            return VisitChildren(context);
        }

        public UstNode VisitDecryption_mechanism([NotNull] TSqlParser.Decryption_mechanismContext context)
        {
            return VisitChildren(context);
        }

        public UstNode VisitXml_type_definition([NotNull] TSqlParser.Xml_type_definitionContext context)
        {
            return VisitChildren(context);
        }

        public UstNode VisitXml_schema_collection([NotNull] TSqlParser.Xml_schema_collectionContext context)
        {
            return VisitChildren(context);
        }

        public UstNode VisitOn_delete([NotNull] TSqlParser.On_deleteContext context)
        {
            return VisitChildren(context);
        }

        public UstNode VisitOn_update([NotNull] TSqlParser.On_updateContext context)
        {
            return VisitChildren(context);
        }

        public UstNode VisitDeclare_set_cursor_common_partial([NotNull] TSqlParser.Declare_set_cursor_common_partialContext context)
        {
            return VisitChildren(context);
        }

        public UstNode VisitUdt_method_arguments([NotNull] TSqlParser.Udt_method_argumentsContext context)
        {
            return VisitChildren(context);
        }

        public UstNode VisitAsterisk([NotNull] TSqlParser.AsteriskContext context)
        {
            return VisitChildren(context);
        }

        public UstNode VisitColumn_elem([NotNull] TSqlParser.Column_elemContext context)
        {
            return VisitChildren(context);
        }

        public UstNode VisitUdt_elem([NotNull] TSqlParser.Udt_elemContext context)
        {
            return VisitChildren(context);
        }

        public UstNode VisitExpression_elem([NotNull] TSqlParser.Expression_elemContext context)
        {
            return VisitChildren(context);
        }

        public UstNode VisitOpen_xml([NotNull] TSqlParser.Open_xmlContext context)
        {
            return VisitChildren(context);
        }

        public UstNode VisitSchema_declaration([NotNull] TSqlParser.Schema_declarationContext context)
        {
            return VisitChildren(context);
        }

        public UstNode VisitColumn_declaration([NotNull] TSqlParser.Column_declarationContext context)
        {
            return VisitChildren(context);
        }

        public UstNode VisitPivot_clause([NotNull] TSqlParser.Pivot_clauseContext context)
        {
            return VisitChildren(context);
        }

        public UstNode VisitUnpivot_clause([NotNull] TSqlParser.Unpivot_clauseContext context)
        {
            return VisitChildren(context);
        }

        public UstNode VisitFull_column_name_list([NotNull] TSqlParser.Full_column_name_listContext context)
        {
            return VisitChildren(context);
        }

        public UstNode VisitXml_data_type_methods([NotNull] TSqlParser.Xml_data_type_methodsContext context)
        {
            InvocationExpression result = new InvocationExpression();
            return result;
        }

        public UstNode VisitValue_method([NotNull] TSqlParser.Value_methodContext context)
        {
            return VisitChildren(context);
        }

        public UstNode VisitQuery_method([NotNull] TSqlParser.Query_methodContext context)
        {
            return VisitChildren(context);
        }

        public UstNode VisitExist_method([NotNull] TSqlParser.Exist_methodContext context)
        {
            return VisitChildren(context);
        }

        public UstNode VisitModify_method([NotNull] TSqlParser.Modify_methodContext context)
        {
            return VisitChildren(context);
        }

        public UstNode VisitNodes_method([NotNull] TSqlParser.Nodes_methodContext context)
        {
            return VisitChildren(context);
        }

        public UstNode VisitAs_column_alias([NotNull] TSqlParser.As_column_aliasContext context)
        {
            return VisitChildren(context);
        }

        public UstNode VisitColumn_name_list_with_order([NotNull] TSqlParser.Column_name_list_with_orderContext context)
        {
            return VisitChildren(context);
        }

        public UstNode VisitNull_or_default([NotNull] TSqlParser.Null_or_defaultContext context)
        {
            return VisitChildren(context);
        }

        public UstNode VisitBegin_conversation_timer([NotNull] TSqlParser.Begin_conversation_timerContext context)
        {
            return VisitChildren(context);
        }

        public UstNode VisitBegin_conversation_dialog([NotNull] TSqlParser.Begin_conversation_dialogContext context)
        {
            return VisitChildren(context);
        }

        public UstNode VisitContract_name([NotNull] TSqlParser.Contract_nameContext context)
        {
            return VisitChildren(context);
        }

        public UstNode VisitService_name([NotNull] TSqlParser.Service_nameContext context)
        {
            return VisitChildren(context);
        }

        public UstNode VisitEnd_conversation([NotNull] TSqlParser.End_conversationContext context)
        {
            return VisitChildren(context);
        }

        public UstNode VisitWaitfor_conversation([NotNull] TSqlParser.Waitfor_conversationContext context)
        {
            return VisitChildren(context);
        }

        public UstNode VisitGet_conversation([NotNull] TSqlParser.Get_conversationContext context)
        {
            return VisitChildren(context);
        }

        public UstNode VisitQueue_id([NotNull] TSqlParser.Queue_idContext context)
        {
            return VisitChildren(context);
        }

        public UstNode VisitSend_conversation([NotNull] TSqlParser.Send_conversationContext context)
        {
            return VisitChildren(context);
        }

        public UstNode VisitThrow_error_number([NotNull] TSqlParser.Throw_error_numberContext context)
        {
            return VisitChildren(context);
        }

        public UstNode VisitThrow_message([NotNull] TSqlParser.Throw_messageContext context)
        {
            return VisitChildren(context);
        }

        public UstNode VisitThrow_state([NotNull] TSqlParser.Throw_stateContext context)
        {
            return VisitChildren(context);
        }

        public UstNode VisitDrop_relational_or_xml_or_spatial_index([NotNull] TSqlParser.Drop_relational_or_xml_or_spatial_indexContext context)
        {
            return VisitChildren(context);
        }

        public UstNode VisitDrop_backward_compatible_index([NotNull] TSqlParser.Drop_backward_compatible_indexContext context)
        {
            return VisitChildren(context);
        }
    }
}