﻿using System;
using System.Collections.Generic;

namespace BLToolkit.Data.SqlBuilder
{
	public class Sql : ISqlExpression, ITableSource
	{
		#region Init

		public Sql()
		{
			_select  = new SelectClause (this);
			_from    = new FromClause   (this);
			_where   = new WhereClause  (this);
			_groupBy = new GroupByClause(this);
			_having  = new WhereClause  (this);
			_orderBy = new OrderByClause(this);
		}

		#endregion

		#region Column

		public class Column : IEquatable<Column>
		{
			public Column(ISqlExpression expression, string alias)
			{
				if (expression == null) throw new ArgumentNullException("expression");

				_expression = expression;
				_alias      = alias;
			}

			public Column(ISqlExpression expression)
				: this(expression, null)
			{
			}

			private ISqlExpression _expression;
			public  ISqlExpression  Expression
			{
				get { return _expression;  }
				set { _expression = value; }
			}

			private string _alias;
			public  string  Alias
			{
				get { return _alias;  }
				set { _alias = value; }
			}

			public bool Equals(Column other)
			{
				return _alias == other.Alias && _expression.Equals(other._expression);
			}
		}

		#endregion

		#region TableSource

		public class TableSource : ITableSource, IExpressionScannable
		{
			public TableSource(ITableSource source, string alias)
			{
				if (source == null) throw new ArgumentNullException("source");

				_source = source;
				_alias  = alias;
			}

			private ITableSource _source;
			public  ITableSource  Source
			{
				get { return _source;  }
				set { _source = value; }
			}

			private string _alias;
			public  string  Alias
			{
				get { return _alias;  }
				set { _alias = value; }
			}

			public TableSource this[ITableSource table]
			{
				get { return this[table, null]; }
			}

			public TableSource this[ITableSource table, string alias]
			{
				get
				{
					foreach (JoinedTable tj in Joins)
					{
						TableSource ts = tj.Table;

						if (ts.Source == table && (alias == null || ts.Alias == alias))
							return ts;
					}

					return null;
				}
			}

			private List<JoinedTable> _joins = new List<JoinedTable>();
			public  List<JoinedTable>  Joins
			{
				get { return _joins;  }
			}

			#region IExpressionScannable Members

			public void ForEach(Action<ISqlExpression> action)
			{
				foreach (JoinedTable join in Joins)
					((IExpressionScannable)join).ForEach(action);
			}

			#endregion
		}

		#endregion

		#region TableJoin

		public enum JoinType
		{
			Auto,
			Inner,
			Left
		}

		public class JoinedTable : IExpressionScannable
		{
			public JoinedTable(JoinType joinType, TableSource table, bool isWeak)
			{
				_joinType = joinType;
				_table    = table;
				_isWeak   = isWeak;
			}

			public JoinedTable(JoinType joinType, ITableSource table, string alias, bool isWeak)
				: this(joinType, new TableSource(table, alias), isWeak)
			{
			}

			private JoinType _joinType;
			public  JoinType  JoinType
			{
				get { return _joinType;  }
				set { _joinType = value; }
			}

			private TableSource _table;
			public  TableSource  Table
			{
				get { return _table;  }
				set { _table = value; }
			}

			private SearchCondition _condition = new SearchCondition();
			public  SearchCondition  Condition
			{
				get { return _condition;  }
			}

			private bool _isWeak;
			public  bool  IsWeak
			{
				get { return _isWeak;  }
				set { _isWeak = value; }
			}

			#region IExpressionScannable Members

			public void ForEach(Action<ISqlExpression> action)
			{
				((IExpressionScannable)Condition).ForEach(action);
				((IExpressionScannable)Table).    ForEach(action);
			}

			#endregion
		}

		#endregion

		#region Predicate

		public interface IPredicate : IExpressionScannable
		{
		}

		public abstract class Predicate : IPredicate
		{
			public enum Operator
			{
				Equal,          // =     Is the operator used to test the equality between two expressions.
				NotEqual,       // <> != Is the operator used to test the condition of two expressions not being equal to each other.
				Greater,        // >     Is the operator used to test the condition of one expression being greater than the other.
				GreaterOrEqual, // >=    Is the operator used to test the condition of one expression being greater than or equal to the other expression.
				NotGreater,     // !>    Is the operator used to test the condition of one expression not being greater than the other expression.
				Less,           // <     Is the operator used to test the condition of one expression being less than the other.
				LessOrEqual,    // <=    Is the operator used to test the condition of one expression being less than or equal to the other expression.
				NotLess         // !<    Is the operator used to test the condition of one expression not being less than the other expression.
			}

			public abstract class ExprBase : Predicate
			{
				public ExprBase(ISqlExpression exp1)
				{
					_expr1 = exp1;
				}

				readonly ISqlExpression _expr1; public ISqlExpression Expr1 { get { return _expr1; } }

				protected override void ForEach(Action<ISqlExpression> action)
				{
					_expr1.ForEach(action);
				}
			}

			public abstract class NotExprBase : ExprBase
			{
				public NotExprBase(ISqlExpression exp1, bool isNot)
					: base(exp1)
				{
					_isNot = isNot;
				}

				readonly bool _isNot; public bool IsNot { get { return _isNot; } }
			}

			// { expression { = | <> | != | > | >= | ! > | < | <= | !< } expression
			//
			public class ExprExpr : ExprBase
			{
				public ExprExpr(ISqlExpression exp1, Operator op, ISqlExpression exp2)
					: base(exp1)
				{
					_op    = op;
					_expr2 = exp2;
				}

				readonly Operator       _op;    public new Operator   Operator { get { return _op;    } }
				readonly ISqlExpression _expr2; public ISqlExpression Expr2    { get { return _expr2; } }

				protected override void ForEach(Action<ISqlExpression> action)
				{
					base.ForEach(action);
					_expr2.ForEach(action);
				}
			}

			// string_expression [ NOT ] LIKE string_expression [ ESCAPE 'escape_character' ]
			//
			public class Like : NotExprBase
			{
				public Like(ISqlExpression exp1, bool isNot, ISqlExpression exp2, char escape)
					: base(exp1, isNot)
				{
					_expr2  = exp2;
					_escape = escape;
				}

				readonly ISqlExpression _expr2;  public ISqlExpression Expr2  { get { return _expr2;  } }
				readonly char           _escape; public char           Escape { get { return _escape; } }

				protected override void ForEach(Action<ISqlExpression> action)
				{
					base.ForEach(action);
					_expr2.ForEach(action);
				}
			}

			// expression [ NOT ] BETWEEN expression AND expression
			//
			public class Between : NotExprBase
			{
				public Between(ISqlExpression exp1, bool isNot, ISqlExpression exp2, ISqlExpression exp3)
					: base(exp1, isNot)
				{
					_expr2 = exp2;
					_expr3 = exp3;
				}

				readonly ISqlExpression _expr2; public ISqlExpression Expr2 { get { return _expr2; } }
				readonly ISqlExpression _expr3; public ISqlExpression Expr3 { get { return _expr3; } }

				protected override void ForEach(Action<ISqlExpression> action)
				{
					base.ForEach(action);
					_expr2.ForEach(action);
					_expr3.ForEach(action);
				}
			}

			// expression IS [ NOT ] NULL
			//
			public class IsNull : NotExprBase
			{
				public IsNull(ISqlExpression exp1, bool isNot)
					: base(exp1, isNot)
				{
				}
			}

			// expression [ NOT ] IN ( subquery | expression [ ,...n ] )
			//
			public class InSubquery : NotExprBase
			{
				public InSubquery(ISqlExpression exp1, bool isNot, Sql subquery)
					: base(exp1, isNot)
				{
					_subquery = subquery;
				}

				readonly Sql _subquery; public Sql Subquery { get { return _subquery; } }

				protected override void ForEach(Action<ISqlExpression> action)
				{
					base.ForEach(action);
					((ISqlExpression)_subquery).ForEach(action);
				}
			}

			public class InList : NotExprBase
			{
				public InList(ISqlExpression exp1, bool isNot, params ISqlExpression[] values)
					: base(exp1, isNot)
				{
					if (values != null && values.Length > 0)
						_values.AddRange(values);
				}

				readonly List<ISqlExpression> _values = new List<ISqlExpression>();
				public   List<ISqlExpression>  Values { get { return _values; } }

				protected override void ForEach(Action<ISqlExpression> action)
				{
					base.ForEach(action);
					foreach (ISqlExpression expr in _values)
						expr.ForEach(action);
				}
			}

			// CONTAINS ( { column | * } , '< contains_search_condition >' )
			// FREETEXT ( { column | * } , 'freetext_string' )
			// expression { = | <> | != | > | >= | !> | < | <= | !< } { ALL | SOME | ANY } ( subquery )
			// EXISTS ( subquery )

			public class FuncLike : Predicate
			{
				public FuncLike(SqlFunction func)
				{
					_func = func;
				}

				readonly SqlFunction _func; public SqlFunction Function { get { return _func; } }
		
				protected override void ForEach(Action<ISqlExpression> action)
				{
					((ISqlExpression)_func).ForEach(action);
				}
			}

			#region IPredicate Members

			protected abstract void ForEach(Action<ISqlExpression> action);

			void IExpressionScannable.ForEach(Action<ISqlExpression> action)
			{
				ForEach(action);
			}

			#endregion
		}

		#endregion

		#region Condition

		public class Condition
		{
			public Condition(bool isNot, IPredicate predicate)
			{
				_isNot     = isNot;
				_predicate = predicate;
			}

			private bool       _isNot;     public bool       IsNot     { get { return _isNot;     } set { _isNot     = value; } }
			private IPredicate _predicate; public IPredicate Predicate { get { return _predicate; } set { _predicate = value; } }
			private bool       _isOr;      public bool       IsOr      { get { return _isOr;      } set { _isOr      = value; } }
		}

		#endregion

		#region SearchCondition

		public class SearchCondition : IPredicate
		{
			private List<Condition> _conditions = new List<Condition>();
			public  List<Condition>  Conditions
			{
				get { return _conditions; }
			}

			#region IPredicate Members

			void IExpressionScannable.ForEach(Action<ISqlExpression> action)
			{
				foreach (Condition condition in Conditions)
					condition.Predicate.ForEach(action);
			}

			#endregion
		}

		#endregion

		#region ConditionBase

		interface IConditionExpr<T>
		{
			T Expr    (ISqlExpression expr);
			T Field   (Field          field);
			T SubQuery(Sql     sql);
			T Value   (object         value);
		}

		public abstract class ConditionBase<T1,T2> : IConditionExpr<ConditionBase<T1,T2>.Expr_>
			where T1 : ConditionBase<T1,T2>
		{
			public class Expr_
			{
				internal Expr_(ConditionBase<T1,T2> condition, bool isNot, ISqlExpression expr)
				{
					_condition = condition;
					_isNot     = isNot;
					_expr      = expr;
				}

				ConditionBase<T1,T2> _condition;
				bool                 _isNot;
				ISqlExpression       _expr;

				T2 Add(IPredicate predicate)
				{
					_condition.Conditions.Conditions.Add(new Condition(_isNot, predicate));
					return _condition.GetNext();
				}

				#region Predicate.ExprExpr

				public class Op_ : IConditionExpr<T2>
				{
					internal Op_(Expr_ expr, Predicate.Operator op) 
					{
						_expr = expr;
						_op   = op;
					}

					Expr_              _expr;
					Predicate.Operator _op;

					public T2 Expr    (ISqlExpression expr)  { return _expr.Add(new Predicate.ExprExpr(_expr._expr, _op, expr)); }
					public T2 Field   (Field          field) { return Expr(field);                    }
					public T2 SubQuery(Sql     sql)   { return Expr(sql);                      }
					public T2 Value   (object         value) { return Expr(new SqlValue(value));      }

					public T2 All     (Sql     sql)   { return Expr(new SqlFunction.All (sql)); }
					public T2 Some    (Sql     sql)   { return Expr(new SqlFunction.Some(sql)); }
					public T2 Any     (Sql     sql)   { return Expr(new SqlFunction.Any (sql)); }
				}

				public Op_ Equal          { get { return new Op_(this, Predicate.Operator.Equal);          } }
				public Op_ NotEqual       { get { return new Op_(this, Predicate.Operator.NotEqual);       } }
				public Op_ Greater        { get { return new Op_(this, Predicate.Operator.Greater);        } }
				public Op_ GreaterOrEqual { get { return new Op_(this, Predicate.Operator.GreaterOrEqual); } }
				public Op_ NotGreater     { get { return new Op_(this, Predicate.Operator.NotGreater);     } }
				public Op_ Less           { get { return new Op_(this, Predicate.Operator.Less);           } }
				public Op_ LessOrEqual    { get { return new Op_(this, Predicate.Operator.LessOrEqual);    } }
				public Op_ NotLess        { get { return new Op_(this, Predicate.Operator.NotLess);        } }

				#endregion

				#region Predicate.Like

				public T2 Like(ISqlExpression expression, char escape) { return Add(new Predicate.Like(_expr, false, expression, escape)); }
				public T2 Like(ISqlExpression expression)              { return Like(expression, '\x0'); }
				public T2 Like(string expression,         char escape) { return Like(new SqlValue(expression), escape); }
				public T2 Like(string expression)                      { return Like(new SqlValue(expression), '\x0'); }

				#endregion

				#region Predicate.Between

				public T2 Between   (ISqlExpression expr1, ISqlExpression expr2) { return Add(new Predicate.Between(_expr, false, expr1, expr2)); }
				public T2 NotBetween(ISqlExpression expr1, ISqlExpression expr2) { return Add(new Predicate.Between(_expr, true,  expr1, expr2)); }

				#endregion

				#region Predicate.IsNull

				public T2 IsNull    { get { return Add(new Predicate.IsNull(_expr, false)); } }
				public T2 IsNotNull { get { return Add(new Predicate.IsNull(_expr, true));  } }

				#endregion

				#region Predicate.In

				public T2 In   (Sql sql) { return Add(new Predicate.InSubquery(_expr, false, sql)); }
				public T2 NotIn(Sql sql) { return Add(new Predicate.InSubquery(_expr, true,  sql)); }

				Predicate.InList CreateInList(bool isNot, object[] exprs)
				{
					Predicate.InList list = new Predicate.InList(_expr, isNot, null);

					if (exprs != null && exprs.Length > 0)
					{
						foreach (object item in exprs)
						{
							if (item == null || item is SqlValue && ((SqlValue)item).Value == null)
								continue;

							if (item is ISqlExpression)
								list.Values.Add((ISqlExpression)item);
							else
								list.Values.Add(new SqlValue(item));
						}
					}

					return list;
				}

				public T2 In   (params object[] exprs) { return Add(CreateInList(false, exprs)); }
				public T2 NotIn(params object[] exprs) { return Add(CreateInList(true,  exprs)); }

				#endregion
			}

			public class Not_ : IConditionExpr<Expr_>
			{
				internal Not_(ConditionBase<T1,T2> condition)
				{
					_condition = condition;
				}

				ConditionBase<T1,T2> _condition;

				public Expr_ Expr    (ISqlExpression expr)  { return new Expr_(_condition, true, expr);  }
				public Expr_ Field   (Field          field) { return Expr(field);               }
				public Expr_ SubQuery(Sql     sql)   { return Expr(sql);                 }
				public Expr_ Value   (object         value) { return Expr(new SqlValue(value)); }

				public T2 Exists(Sql subQuery)
				{
					_condition.Conditions.Conditions.Add(new Condition(true, new Predicate.FuncLike(new SqlFunction.Exists(subQuery))));
					return _condition.GetNext();
				}
			}

			protected abstract SearchCondition Conditions { get; }
			protected abstract T2              GetNext();

			protected T1 SetOr(bool value)
			{
				Conditions.Conditions[Conditions.Conditions.Count - 1].IsOr = value;
				return (T1)this;
			}

			public Not_  Not { get { return new Not_(this); } }

			public Expr_ Expr    (ISqlExpression expr)  { return new Expr_(this, false, expr);  }
			public Expr_ Field   (Field          field) { return Expr(field);               }
			public Expr_ SubQuery(Sql     sql)   { return Expr(sql);                 }
			public Expr_ Value   (object         value) { return Expr(new SqlValue(value)); }

			public T2 Exists(Sql subQuery)
			{
				Conditions.Conditions.Add(new Condition(false, new Predicate.FuncLike(new SqlFunction.Exists(subQuery))));
				return this.GetNext();
			}
		}

		#endregion

		#region OrderByItem

		public class OrderByItem
		{
			public OrderByItem(ISqlExpression expression, bool isDescending)
			{
				_expression   = expression;
				_isDescending = isDescending;
			}

			private ISqlExpression _expression;   public ISqlExpression Expression   { get { return _expression;   } }
			private bool           _isDescending; public bool           IsDescending { get { return _isDescending; } }
		}

		#endregion

		#region ClauseBase

		public abstract class ClauseBase
		{
			protected ClauseBase(Sql sql)
			{
				_sql = sql;
			}

			public SelectClause  Select  { get { return Sql.Select;  } }
			public FromClause    From    { get { return Sql.From;    } }
			public WhereClause   Where   { get { return Sql.Where;   } }
			public GroupByClause GroupBy { get { return Sql.GroupBy; } }
			public WhereClause   Having  { get { return Sql.Having;  } }
			public OrderByClause OrderBy { get { return Sql.OrderBy; } }
			public Sql           End()   { return Sql; }

			readonly  Sql _sql;
			protected Sql  Sql
			{
				get { return _sql; }
			}
		}

		public abstract class ClauseBase<T1, T2> : ConditionBase<T1, T2>
			where T1 : ClauseBase<T1, T2>
		{
			protected ClauseBase(Sql sql)
			{
				_sql = sql;
			}

			public SelectClause  Select  { get { return Sql.Select;  } }
			public FromClause    From    { get { return Sql.From;    } }
			public GroupByClause GroupBy { get { return Sql.GroupBy; } }
			public WhereClause   Having  { get { return Sql.Having;  } }
			public OrderByClause OrderBy { get { return Sql.OrderBy; } }
			public Sql           End()   { return Sql; }

			readonly  Sql _sql;
			protected Sql  Sql
			{
				get { return _sql; }
			}
		}

		#endregion

		#region SelectClause

		public class SelectClause : ClauseBase, IExpressionScannable
		{
			#region Init

			internal SelectClause(Sql sql) : base(sql)
			{
			}

			#endregion

			#region Columns

			public SelectClause Field(Field field)
			{
				AddOrGetColumn(new Column(field));
				return this;
			}

			public SelectClause Field(Field field, string alias)
			{
				AddOrGetColumn(new Column(field, alias));
				return this;
			}

			public SelectClause SubQuery(Sql sql)
			{
				AddOrGetColumn(new Column(sql));
				return this;
			}

			public SelectClause SubQuery(Sql sql, string alias)
			{
				AddOrGetColumn(new Column(sql, alias));
				return this;
			}

			public SelectClause Expr(ISqlExpression expr)
			{
				AddOrGetColumn(new Column(expr));
				return this;
			}

			public SelectClause Expr(ISqlExpression expr, string alias)
			{
				AddOrGetColumn(new Column(expr, alias));
				return this;
			}

			public SelectClause Expr(string expr, params ISqlExpression[] values)
			{
				AddOrGetColumn(new Column(new SqlExpression(expr, values)));
				return this;
			}

			public SelectClause Expr(string alias, string expr, params ISqlExpression[] values)
			{
				AddOrGetColumn(new Column(new SqlExpression(expr, values)));
				return this;
			}

			Column AddOrGetColumn(Column col)
			{
				foreach (Column c in Columns)
					if (c.Equals(col))
						return col;

				//col.Index = SelectList.Count;

				Columns.Add(col);

				return col;
			}

			readonly List<Column> _columns = new List<Column>();
			public   List<Column>  Columns
			{
				get { return _columns; }
			}

			#endregion

			#region Distinct

			public SelectClause Distinct
			{
				get { _isDistinct = true; return this; }
			}

			private bool _isDistinct;
			private bool  IsDistinct { get { return _isDistinct; } set { _isDistinct = value; } }

			#endregion

			#region Take

			public SelectClause Take(int value)
			{
				_takeValue = value;
				return this;
			}

			private int _takeValue;
			private int  TakeValue { get { return _takeValue; } set { _takeValue = value; } }

			#endregion

			#region Skip

			public SelectClause Skip(int value)
			{
				_skipValue = value;
				return this;
			}

			private int _skipValue;
			private int  SkipeValue { get { return _skipValue; } set { _skipValue = value; } }

			#endregion

			#region IExpressionScannable Members

			void IExpressionScannable.ForEach(Action<ISqlExpression> action)
			{
				foreach (Column column in Columns)
					column.Expression.ForEach(action);
			}

			#endregion
		}

		readonly SelectClause _select;
		public   SelectClause  Select
		{
			get { return _select; }
		}

		#endregion

		#region FromClause

		public class FromClause : ClauseBase, IExpressionScannable
		{
			#region FromTable

			interface INextJoin
			{
				Table_       Table        (ITableSource table);
				Table_       Table        (ITableSource table, string alias);

				Table_.Join_ InnerJoin    (ITableSource table);
				Table_.Join_ InnerJoin    (ITableSource table, string alias);
				Table_.Join_ LeftJoin     (ITableSource table);
				Table_.Join_ LeftJoin     (ITableSource table, string alias);
				Table_.Join_ Join         (ITableSource table);
				Table_.Join_ Join         (ITableSource table, string alias);

				Table_.Join_ WeakInnerJoin(ITableSource table);
				Table_.Join_ WeakInnerJoin(ITableSource table, string alias);
				Table_.Join_ WeakLeftJoin (ITableSource table);
				Table_.Join_ WeakLeftJoin (ITableSource table, string alias);
				Table_.Join_ WeakJoin     (ITableSource table);
				Table_.Join_ WeakJoin     (ITableSource table, string alias);
			}

			public class Table_ : ClauseBase, INextJoin
			{
				#region Join

				public class Join_ : Table_
				{
					#region On

					public class On_ : ConditionBase<On_, On_.Next>
					{
						public class Next : ClauseBase, INextJoin
						{
							internal Next(On_ parent) : base(parent._parent.Sql)
							{
								_parent = parent;
							}

							On_ _parent;

							public On_ Or  { get { return _parent.SetOr(true);  } }
							public On_ And { get { return _parent.SetOr(false); } }

							public Table_ Table        (ITableSource table)               { return Sql.From.Table(table);        }
							public Table_ Table        (ITableSource table, string alias) { return Sql.From.Table(table, alias); }

							public Join_  InnerJoin    (ITableSource table)               { return _parent._parent._parent.InnerJoin    (table);        }
							public Join_  InnerJoin    (ITableSource table, string alias) { return _parent._parent._parent.InnerJoin    (table, alias); }
							public Join_  LeftJoin     (ITableSource table)               { return _parent._parent._parent.LeftJoin     (table);        }
							public Join_  LeftJoin     (ITableSource table, string alias) { return _parent._parent._parent.LeftJoin     (table, alias); }
							public Join_  Join         (ITableSource table)               { return _parent._parent._parent.Join         (table);        }
							public Join_  Join         (ITableSource table, string alias) { return _parent._parent._parent.InnerJoin    (table, alias); }

							public Join_  WeakInnerJoin(ITableSource table)               { return _parent._parent._parent.WeakInnerJoin(table);        }
							public Join_  WeakInnerJoin(ITableSource table, string alias) { return _parent._parent._parent.WeakInnerJoin(table, alias); }
							public Join_  WeakLeftJoin (ITableSource table)               { return _parent._parent._parent.WeakLeftJoin (table);        }
							public Join_  WeakLeftJoin (ITableSource table, string alias) { return _parent._parent._parent.WeakLeftJoin (table, alias); }
							public Join_  WeakJoin     (ITableSource table)               { return _parent._parent._parent.WeakJoin     (table);        }
							public Join_  WeakJoin     (ITableSource table, string alias) { return _parent._parent._parent.WeakInnerJoin(table, alias); }
						}

						internal On_(Join_ parent)
						{
							_parent = parent;
						}

						Join_ _parent;

						protected override SearchCondition Conditions
						{
							get { return _parent._join.Condition; }
						}

						protected override On_.Next GetNext()
						{
							return new Next(this);
						}

						public Table_ Default { get { return _parent._parent; } }
					}

					#endregion

					internal Join_(Table_ parent, JoinedTable join)
						: base(parent.Sql, join.Table)
					{
						_parent = parent;
						_join   = join;
					}

					Table_      _parent;
					JoinedTable _join;

					public On_ On
					{
						get { return new On_(this); }
					}
				}

				#endregion

				internal Table_(Sql sql, TableSource table)
					: base(sql)
				{
					_table = table;
				}

				private TableSource _table;

				public Table_ Table        (ITableSource table)               { return Sql.From.Table(table);        }
				public Table_ Table        (ITableSource table, string alias) { return Sql.From.Table(table, alias); }

				public Join_  InnerJoin    (ITableSource table)               { return AddJoin(JoinType.Inner, table, null,  false); }
				public Join_  InnerJoin    (ITableSource table, string alias) { return AddJoin(JoinType.Inner, table, alias, false); }
				public Join_  LeftJoin     (ITableSource table)               { return AddJoin(JoinType.Left,  table, null,  false); }
				public Join_  LeftJoin     (ITableSource table, string alias) { return AddJoin(JoinType.Left,  table, alias, false); }
				public Join_  Join         (ITableSource table)               { return AddJoin(JoinType.Auto,  table, null,  false); }
				public Join_  Join         (ITableSource table, string alias) { return AddJoin(JoinType.Auto,  table, alias, false); }

				public Join_  WeakInnerJoin(ITableSource table)               { return AddJoin(JoinType.Inner, table, null,  true); }
				public Join_  WeakInnerJoin(ITableSource table, string alias) { return AddJoin(JoinType.Inner, table, alias, true); }
				public Join_  WeakLeftJoin (ITableSource table)               { return AddJoin(JoinType.Left,  table, null,  true); }
				public Join_  WeakLeftJoin (ITableSource table, string alias) { return AddJoin(JoinType.Left,  table, alias, true); }
				public Join_  WeakJoin     (ITableSource table)               { return AddJoin(JoinType.Auto,  table, null,  true); }
				public Join_  WeakJoin     (ITableSource table, string alias) { return AddJoin(JoinType.Auto,  table, alias, true); }

				Join_ AddJoin(JoinType joinType, ITableSource table, string alias, bool isWeak)
				{
					foreach (JoinedTable tj in _table.Joins)
					{
						TableSource ts = tj.Table;

						if (ts.Source == table)
						{
							if (alias != null && ts.Alias != alias) throw new ArgumentException("alias");
							if (tj.JoinType != joinType)            throw new ArgumentException("joinType");
							if (tj.IsWeak != isWeak)                throw new ArgumentException("isWeak");

							return new Join_(this, tj);
						}
					}

					if (Sql.From[table, alias] != null)
						throw new ArgumentException("alias");

					JoinedTable join = new JoinedTable(joinType, table, alias, isWeak);

					_table.Joins.Add(join);

					return new Join_(this, join);
				}
			}

			#endregion

			internal FromClause(Sql sql) : base(sql)
			{
			}

			public Table_ Table(ITableSource table)
			{
				return Table(table, null);
			}

			public Table_ Table(ITableSource table, string alias)
			{
				return new Table_(Sql, AddOrGetTable(table, alias));
			}

			TableSource AddOrGetTable(ITableSource table, string alias)
			{
				foreach (TableSource ts in Tables)
					if (ts.Source == table)
						if (alias == null || ts.Alias == alias)
							return ts;
						else
							throw new ArgumentException("alias");

				TableSource t = new TableSource(table, alias);

				Tables.Add(t);

				return t;
			}

			public TableSource this[ITableSource table]
			{
				get { return this[table, null]; }
			}

			public TableSource this[ITableSource table, string alias]
			{
				get
				{
					foreach (TableSource ts in Tables)
					{
						if (ts.Source == table && (alias == null || ts.Alias == alias))
							return ts;

						TableSource join = ts[table, alias];

						if (join != null)
							return join;
					}

					return null;
				}
			}

			private List<TableSource> _tables = new List<TableSource>();
			public  List<TableSource>  Tables
			{
				get { return _tables; }
			}

			#region IExpressionScannable Members

			void IExpressionScannable.ForEach(Action<ISqlExpression> action)
			{
				foreach (TableSource table in Tables)
					((IExpressionScannable)table).ForEach(action);
			}

			#endregion
		}

		readonly FromClause _from;
		public   FromClause  From
		{
			get { return _from; }
		}

		#endregion

		#region WhereClause

		public class WhereClause : ClauseBase<WhereClause, WhereClause.Next>, IExpressionScannable
		{
			public class Next : ClauseBase
			{
				internal Next(WhereClause parent) : base(parent.Sql)
				{
					_parent = parent;
				}

				WhereClause _parent;

				public WhereClause Or  { get { return _parent.SetOr(true);  } }
				public WhereClause And { get { return _parent.SetOr(false); } }
			}

			internal WhereClause(Sql sql) : base(sql)
			{
			}

			private SearchCondition _searchCondition = new SearchCondition();
			public  SearchCondition  SearchCondition
			{
				get { return _searchCondition; }
			}

			protected override SearchCondition Conditions
			{
				get { return _searchCondition; }
			}

			protected override WhereClause.Next GetNext()
			{
				return new Next(this);
			}

			#region IExpressionScannable Members

			void IExpressionScannable.ForEach(Action<ISqlExpression> action)
			{
				((IExpressionScannable)SearchCondition).ForEach(action);
			}

			#endregion
		}

		readonly WhereClause _where;
		public   WhereClause  Where
		{
			get { return _where; }
		}

		#endregion

		#region GroupByClause

		public class GroupByClause : ClauseBase, IExpressionScannable
		{
			internal GroupByClause(Sql sql) : base(sql)
			{
			}

			public GroupByClause Expr(ISqlExpression expr)
			{
				Add(expr);
				return this;
			}

			public GroupByClause Field(Field field)
			{
				return Expr(field);
			}

			void Add(ISqlExpression expr)
			{
				foreach (ISqlExpression e in Items)
					if (e.Equals(expr))
						return;

				Items.Add(expr);
			}

			private List<ISqlExpression> _items = new List<ISqlExpression>();
			public  List<ISqlExpression>  Items
			{
				get { return _items; }
			}

			#region IExpressionScannable Members

			void IExpressionScannable.ForEach(Action<ISqlExpression> action)
			{
				foreach (ISqlExpression item in Items)
					item.ForEach(action);
			}

			#endregion
		}

		readonly GroupByClause _groupBy;
		public   GroupByClause  GroupBy
		{
			get { return _groupBy; }
		}

		#endregion

		#region HavingClause

		readonly WhereClause _having;
		public   WhereClause  Having
		{
			get { return _having; }
		}

		#endregion

		#region OrderByClause

		public class OrderByClause : ClauseBase, IExpressionScannable
		{
			internal OrderByClause(Sql sql) : base(sql)
			{
			}

			public OrderByClause Expr(ISqlExpression expr, bool isDescending)
			{
				Add(expr, isDescending);
				return this;
			}

			public OrderByClause Expr     (ISqlExpression expr)            { return Expr(expr,  false);        }
			public OrderByClause ExprAsc  (ISqlExpression expr)            { return Expr(expr,  false);        }
			public OrderByClause ExprDesc (ISqlExpression expr)            { return Expr(expr,  true);         }
			public OrderByClause Field    (Field field, bool isDescending) { return Expr(field, isDescending); }
			public OrderByClause Field    (Field field)                    { return Expr(field, false);        }
			public OrderByClause FieldAsc (Field field)                    { return Expr(field, false);        }
			public OrderByClause FieldDesc(Field field)                    { return Expr(field, true);         }

			void Add(ISqlExpression expr, bool isDescending)
			{
				foreach (OrderByItem item in Items)
					if (item.Expression.Equals(expr))
						return;

				Items.Add(new OrderByItem(expr, isDescending));
			}

			private List<OrderByItem> _items = new List<OrderByItem>();
			public  List<OrderByItem>  Items
			{
				get { return _items; }
			}

			#region IExpressionScannable Members

			void IExpressionScannable.ForEach(Action<ISqlExpression> action)
			{
				foreach (OrderByItem item in Items)
					item.Expression.ForEach(action);
			}

			#endregion
		}

		readonly OrderByClause _orderBy;
		public   OrderByClause  OrderBy
		{
			get { return _orderBy; }
		}

		#endregion

		#region FinalizeAndValidate

		public void FinalizeAndValidate()
		{
			ResolveWeakJoins();
		}

		delegate bool FindTableSource(TableSource table);

		void ResolveWeakJoins()
		{
			List<ITableSource> tables = null;

			FindTableSource findTable = null; findTable = delegate(TableSource table)
			{
				if (tables.Contains(table.Source))
					return true;

				foreach (JoinedTable join in table.Joins)
					if (findTable(join.Table))
					{
						join.IsWeak = false;
						return true;
					}

				return false;
			};

			Action<TableSource> doTable = null; doTable = delegate(TableSource table)
			{
				for (int i = 0; i < table.Joins.Count; i++)
				{
					JoinedTable join = table.Joins[i];

					if (join.IsWeak)
					{
						if (tables == null)
						{
							tables = new List<ITableSource>();

							Action<ISqlExpression> tableCollector = delegate(ISqlExpression expr)
							{
								Field field = expr as Field;

								if (field != null && !tables.Contains(field.Table))
									tables.Add(field.Table);
							};

							((IExpressionScannable)Select) .ForEach(tableCollector);
							((IExpressionScannable)Where)  .ForEach(tableCollector);
							((IExpressionScannable)GroupBy).ForEach(tableCollector);
							((IExpressionScannable)Having) .ForEach(tableCollector);
							((IExpressionScannable)OrderBy).ForEach(tableCollector);
						}

						if (findTable(join.Table))
						{
							join.IsWeak = false;
						}
						else
						{
							table.Joins.RemoveAt(i);
							i--;
							continue;
						}
					}

					doTable(join.Table);
				}
			};

			foreach (TableSource table in From.Tables)
				doTable(table);
		}

		#endregion

		#region IExpressionScannable Members

		void IExpressionScannable.ForEach(Action<ISqlExpression> action)
		{
			action(this);
			((IExpressionScannable)Select) .ForEach(action);
			((IExpressionScannable)From)   .ForEach(action);
			((IExpressionScannable)Where)  .ForEach(action);
			((IExpressionScannable)GroupBy).ForEach(action);
			((IExpressionScannable)Having) .ForEach(action);
			((IExpressionScannable)OrderBy).ForEach(action);
		}

		#endregion

		#region IEquatable<ISqlExpression> Members

		bool IEquatable<ISqlExpression>.Equals(ISqlExpression other)
		{
			return (object)this == other;
		}

		#endregion
	}
}
