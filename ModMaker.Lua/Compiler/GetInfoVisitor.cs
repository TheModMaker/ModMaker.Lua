using ModMaker.Lua.Parser;
using ModMaker.Lua.Parser.Items;
using System;
using System.Linq;

namespace ModMaker.Lua.Compiler
{
    /// <summary>
    /// A visitor that resolves gotos and breaks and also get capture information
    /// about function definitions.  This information is stored in the given
    /// item. This is used by the default compiler.
    /// </summary>
    public sealed class GetInfoVisitor : IParseItemVisitor
    {
        GetInfoTree tree;

        internal NameItem[] GlobalCaptures;
        internal bool GlobalNested;

        /// <summary>
        /// Creates a new instance of GetInfoVisitor.
        /// </summary>
        public GetInfoVisitor()
        {
            this.tree = new GetInfoTree();
        }

        /// <summary>
        /// Resolves all the labels and updates the information in the given 
        /// IParseItem tree.
        /// </summary>
        /// <param name="target">The IParseItem tree to traverse.</param>
        /// <exception cref="System.ArgumentNullException">If target is null.</exception>
        public void Resolve(IParseItem target)
        {
            if (target == null)
                throw new ArgumentNullException("target");

            tree = new GetInfoTree();
            target.Accept(this);
            tree.Resolve();

            var info = tree.EndFunc();
            GlobalCaptures = info.CapturedLocals;
            GlobalNested = info.HasNested;
        }

        /// <summary>
        /// Called when the item is a binary expression item.
        /// </summary>
        /// <param name="target">The object that was passed to IParseItem.Accept.</param>
        /// <returns>The passed target or a modification of it.</returns>
        /// <exception cref="System.ArgumentNullException">If target is null.</exception>
        public IParseItem Visit(BinOpItem target)
        {
            if (target == null)
                throw new ArgumentNullException("target");

            target.Lhs.Accept(this);
            target.Rhs.Accept(this);
            return target;
        }
        /// <summary>
        /// Called when the item is a block item.
        /// </summary>
        /// <param name="target">The object that was passed to IParseItem.Accept.</param>
        /// <returns>The passed target or a modification of it.</returns>
        /// <exception cref="System.ArgumentNullException">If target is null.</exception>
        public IParseItem Visit(BlockItem target)
        {
            if (target == null)
                throw new ArgumentNullException("target");

            using (tree.Block(true))
            {
                foreach (var item in target.Children)
                    item.Accept(this);

                if (target.Return != null)
                    target.Return.Accept(this);
            }
            return target;
        }
        /// <summary>
        /// Called when the item is a class definition item.
        /// </summary>
        /// <param name="target">The object that was passed to IParseItem.Accept.</param>
        /// <returns>The passed target or a modification of it.</returns>
        public IParseItem Visit(ClassDefItem target)
        {
            // Do nothing.
            return target;
        }
        /// <summary>
        /// Called when the item is a for generic item.
        /// </summary>
        /// <param name="target">The object that was passed to IParseItem.Accept.</param>
        /// <returns>The passed target or a modification of it.</returns>
        /// <exception cref="System.ArgumentNullException">If target is null.</exception>
        public IParseItem Visit(ForGenItem target)
        {
            if (target == null)
                throw new ArgumentNullException("target");

            using (tree.Block(true))
            {
                tree.DefineLocal(target.Names);
                tree.DefineLabel(target.Break);

                target.Block.Accept(this);
                foreach (var item in target.Expressions)
                    item.Accept(this);
            }

            return target;
        }
        /// <summary>
        /// Called when the item is a for numerical item.
        /// </summary>
        /// <param name="target">The object that was passed to IParseItem.Accept.</param>
        /// <returns>The passed target or a modification of it.</returns>
        /// <exception cref="System.ArgumentNullException">If target is null.</exception>
        public IParseItem Visit(ForNumItem target)
        {
            if (target == null)
                throw new ArgumentNullException("target");

            using (tree.Block(true))
            {
                tree.DefineLocal(new[] { target.Name });
                tree.DefineLabel(target.Break);

                target.Block.Accept(this);
                if (target.Start != null)
                    target.Start.Accept(this);
                if (target.Limit != null)
                    target.Limit.Accept(this);
                if (target.Step != null)
                    target.Step.Accept(this);
            }
            return target;
        }
        /// <summary>
        /// Called when the item is a function call item.
        /// </summary>
        /// <param name="target">The object that was passed to IParseItem.Visit.</param>
        /// <returns>The passed target or a modification of it.</returns>
        /// <exception cref="System.ArgumentNullException">If target is null.</exception>
        public IParseItem Visit(FuncCallItem target)
        {
            if (target == null)
                throw new ArgumentNullException("target");

            foreach (var item in target.Arguments)
                item.Expression.Accept(this);
            target.Prefix.Accept(this);

            return target;
        }
        /// <summary>
        /// Called when the item is a function definition item.
        /// </summary>
        /// <param name="target">The object that was passed to IParseItem.Visit.</param>
        /// <returns>The passed target or a modification of it.</returns>
        /// <exception cref="System.ArgumentNullException">If target is null.</exception>
        public IParseItem Visit(FuncDefItem target)
        {
            if (target == null)
                throw new ArgumentNullException("target");

            if (target.Local)
            {
                tree.DefineLocal(new[] { target.Prefix as NameItem });
            }

            using (tree.DefineFunc())
            {
                tree.DefineLocal(target.Arguments);
                target.Block.Accept(this);
            }
            target.FunctionInformation = tree.EndFunc();

            return target;
        }
        /// <summary>
        /// Called when the item is a goto item.
        /// </summary>
        /// <param name="target">The object that was passed to IParseItem.Visit.</param>
        /// <returns>The passed target or a modification of it.</returns>
        /// <exception cref="System.ArgumentNullException">If target is null.</exception>
        public IParseItem Visit(GotoItem target)
        {
            if (target == null)
                throw new ArgumentNullException("target");

            tree.DefineGoto(target);
            return target;
        }
        /// <summary>
        /// Called when the item is an if item.
        /// </summary>
        /// <param name="target">The object that was passed to IParseItem.Visit.</param>
        /// <returns>The passed target or a modification of it.</returns>
        /// <exception cref="System.ArgumentNullException">If target is null.</exception>
        public IParseItem Visit(IfItem target)
        {
            if (target == null)
                throw new ArgumentNullException("target");

            target.Exp.Accept(this);

            using (tree.Block(true))
                target.Block.Accept(this);

            for (int i = 0; i < target.Elses.Count; i++)
            {
                using (tree.Block(true))
                    target.Elses[i].Expression.Accept(this);
            }

            if (target.ElseBlock != null)
            {
                using (tree.Block(true))
                    target.ElseBlock.Accept(this);
            }

            return target;
        }
        /// <summary>
        /// Called when the item is an indexer item.
        /// </summary>
        /// <param name="target">The object that was passed to IParseItem.Visit.</param>
        /// <returns>The passed target or a modification of it.</returns>
        /// <exception cref="System.ArgumentNullException">If target is null.</exception>
        public IParseItem Visit(IndexerItem target)
        {
            if (target == null)
                throw new ArgumentNullException("target");

            target.Prefix.Accept(this);
            target.Expression.Accept(this);

            return target;
        }
        /// <summary>
        /// Called when the item is a label item.
        /// </summary>
        /// <param name="target">The object that was passed to IParseItem.Visit.</param>
        /// <returns>The passed target or a modification of it.</returns>
        /// <exception cref="System.ArgumentNullException">If target is null.</exception>
        public IParseItem Visit(LabelItem target)
        {
            if (target == null)
                throw new ArgumentNullException("target");

            tree.DefineLabel(target);

            return target;
        }
        /// <summary>
        /// Called when the item is a literal item.
        /// </summary>
        /// <param name="target">The object that was passed to IParseItem.Visit.</param>
        /// <returns>The passed target or a modification of it.</returns>
        public IParseItem Visit(LiteralItem target)
        {
            // Do nothing.
            return target;
        }
        /// <summary>
        /// Called when the item is a name item.
        /// </summary>
        /// <param name="target">The object that was passed to IParseItem.Visit.</param>
        /// <returns>The passed target or a modification of it.</returns>
        /// <exception cref="System.ArgumentNullException">If target is null.</exception>
        public IParseItem Visit(NameItem target)
        {
            if (target == null)
                throw new ArgumentNullException("target");

            tree.GetName(target);

            return target;
        }
        /// <summary>
        /// Called when the item is a repeat item.
        /// </summary>
        /// <param name="target">The object that was passed to IParseItem.Visit.</param>
        /// <returns>The passed target or a modification of it.</returns>
        /// <exception cref="System.ArgumentNullException">If target is null.</exception>
        public IParseItem Visit(RepeatItem target)
        {
            if (target == null)
                throw new ArgumentNullException("target");

            target.Expression.Accept(this);

            using (tree.Block(true))
            {
                tree.DefineLabel(target.Break);
                target.Block.Accept(this);
            }

            return target;
        }
        /// <summary>
        /// Called when the item is a return item.
        /// </summary>
        /// <param name="target">The object that was passed to IParseItem.Visit.</param>
        /// <returns>The passed target or a modification of it.</returns>
        /// <exception cref="System.ArgumentNullException">If target is null.</exception>
        public IParseItem Visit(ReturnItem target)
        {
            if (target == null)
                throw new ArgumentNullException("target");

            foreach (var item in target.Expressions)
                item.Accept(this);

            return target;
        }
        /// <summary>
        /// Called when the item is a table item.
        /// </summary>
        /// <param name="target">The object that was passed to IParseItem.Visit.</param>
        /// <returns>The passed target or a modification of it.</returns>
        /// <exception cref="System.ArgumentNullException">If target is null.</exception>
        public IParseItem Visit(TableItem target)
        {
            if (target == null)
                throw new ArgumentNullException("target");

            foreach (var item in target.Fields)
            {
                item.Key.Accept(this);
                item.Value.Accept(this);
            }

            return target;
        }
        /// <summary>
        /// Called when the item is a unary operation item.
        /// </summary>
        /// <param name="target">The object that was passed to IParseItem.Visit.</param>
        /// <returns>The passed target or a modification of it.</returns>
        /// <exception cref="System.ArgumentNullException">If target is null.</exception>
        public IParseItem Visit(UnOpItem target)
        {
            if (target == null)
                throw new ArgumentNullException("target");

            target.Target.Accept(this);

            return target;
        }
        /// <summary>
        /// Called when the item is an assignment item.
        /// </summary>
        /// <param name="target">The object that was passed to IParseItem.Visit.</param>
        /// <returns>The passed target or a modification of it.</returns>
        /// <exception cref="System.ArgumentNullException">If target is null.</exception>
        public IParseItem Visit(AssignmentItem target)
        {
            if (target == null)
                throw new ArgumentNullException("target");

            if (target.Local)
            {
                tree.DefineLocal(target.Names.Select(i => i as NameItem));
            }
            else
            {
                foreach (var item in target.Names)
                    item.Accept(this);
            }

            foreach (var item in target.Expressions)
                item.Accept(this);

            return target;
        }
        /// <summary>
        /// Called when the item is a while item.
        /// </summary>
        /// <param name="target">The object that was passed to IParseItem.Visit.</param>
        /// <returns>The passed target or a modification of it.</returns>
        /// <exception cref="System.ArgumentNullException">If target is null.</exception>
        public IParseItem Visit(WhileItem target)
        {
            if (target == null)
                throw new ArgumentNullException("target");

            target.Exp.Accept(this);

            using (tree.Block(true))
            {
                tree.DefineLabel(target.Break);
                target.Block.Accept(this);
            }

            return target;
        }
    }
}
