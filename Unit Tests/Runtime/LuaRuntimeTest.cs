// Copyright 2014 Jacob Trimble
//
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
//
//     http://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.

using ModMaker.Lua.Runtime;
using System;
using System.Reflection;
using System.Collections;
using System.Text;
using System.Linq;
using System.IO;
using ModMaker.Lua;
using System.Collections.Generic;

namespace UnitTests.Net
{
    // TODO: Fix tests.

    /// <summary>
    ///This is a test class for LuaRuntimeTest and is intended
    ///to contain all LuaRuntimeTest Unit Tests
    ///</summary>
    /*[TestClass()]
    public class LuaRuntimeTest
    {
        private TestContext testContextInstance;

        /// <summary>
        ///Gets or sets the test context which provides
        ///information about and functionality for the current test run.
        ///</summary>
        public TestContext TestContext
        {
            get
            {
                return testContextInstance;
            }
            set
            {
                testContextInstance = value;
            }
        }

        #region Additional test attributes
        //
        //You can use the following additional attributes as you write your tests:
        //
        //Use ClassInitialize to run code before running the first test in the class
        //[ClassInitialize()]
        //public static void MyClassInitialize(TestContext testContext)
        //{
        //}
        //
        //Use ClassCleanup to run code after all tests in a class have run
        //[ClassCleanup()]
        //public static void MyClassCleanup()
        //{
        //}
        //
        //Use TestInitialize to run code before running each test
        //[TestInitialize()]
        //public void MyTestInitialize()
        //{
        //}
        //
        //Use TestCleanup to run code after each test has run
        //[TestCleanup()]
        //public void MyTestCleanup()
        //{
        //}
        //
        #endregion

        /// <summary>
        /// Contains several user-defined types to test ConvertType and
        /// GetCompatibleMethod when using user-defined types and casts.
        /// </summary>
        static class UserTypes
        {
            // This is the type diagram for the types:
            //
            //
            //                     BaseInterface
            //                    /    |     |   \
            //                  /      |     |     \
            // DerivedInterface1       |     |     DerivedInterface2
            //   |                     |     |             |
            // IgnoreOnClass  BaseClassAlso  BaseClass     |
            //   |                |                 |      |
            // IgnoreDerived  DerivedClassAlso   DerivedClass
            //                   |                  |
            //               RootClassAlso       RootClass


            public interface BaseInterface { }
            public interface DerivedInterface1 : BaseInterface { }
            public interface DerivedInterface2 : BaseInterface { }

            public class BaseClass : BaseInterface
            {
                public string Day;
                public object Test;

                public string Prop { get; set; }

                public object this[int i]
                {
                    get { return ""; }
                    set { Test = value; }
                }

                public void Some() { }
            }
            public class DerivedClass : BaseClass, DerivedInterface2
            {
                public void Foo() { }
                public virtual void Foo(int i) { }
                public virtual void Foo(long l) { }
                public virtual void Foo(string s) { }
                public void Foo(IEnumerable ie) { }
                public void Foo(BaseClass b) { }
                public void Foo(DerivedClass d) { }
                public void Foo(DerivedClass d, int i) { }
                public void Foo(RootClass r) { }
                public void Foo(object o) { }
                public void Foo(object o1, object o2) { }
                public void Foo(int i, string s) { }
                public void Foo(string s, int i) { }
                public void Foo(int i, int i2, params string[] p) { }
                public void Foo(int i, int i2, params object[] p) { }
                public void Foo(object o1, object o2, params object[] p) { }
                public void Foo(object o1, object o2, string s, params object[] p) { }
                public void Foo(object o1, object o2, int l, params int[] arr) { }
                public void Foo(object o1, object o2, int l, int d = 22, int ii = 2) { }

                private void Foo(string s, string s1) { }
                private void Foo(short s) { }
                [LuaIgnore]
                public void Foo(string s, short i) { }
                [LuaIgnore]
                public void Foo(BaseInterface ie) { }

                public void Foo2(object o) { }
                public void Foo2(ref object o) { }
                public void Foo2(object o, ref string s) { }
                public void Foo2(object o, ref object o2) { }

                public void Foo3(string s) { }
                public void Foo3(LuaUserData<string> da) { }
                public void Foo3(int i, string s) { }
                public void Foo3(int i, LuaUserData ud) { }
                public void Foo3(int i, LuaUserData<string> ud) { }
            }
            public class RootClass : DerivedClass
            {
                [LuaIgnore]
                public override void Foo(int i) { }
                [LuaIgnore]
                public override void Foo(long l) { }
                [LuaIgnore]
                public override void Foo(string s) { }

                [LuaIgnore]
                public override string ToString()
                {
                    return base.ToString();
                }
            }

            public class BaseClassAlso : BaseInterface
            {
                // this is used for testing accessibility within Lua, this
                //   field is never used locally.
#pragma warning disable 0169
                string hidden;
#pragma warning restore 0169
            }
            public class DerivedClassAlso : BaseClassAlso
            {
                public static explicit operator DerivedClass(DerivedClassAlso obj)
                {
                    return new DerivedClass();
                }
                public static explicit operator DerivedClassAlso(DerivedClass obj)
                {
                    return new DerivedClassAlso();
                }
            }
            public class RootClassAlso : DerivedClassAlso
            {
                public static implicit operator RootClass(RootClassAlso obj)
                {
                    return new RootClass();
                }
                public static implicit operator RootClassAlso(RootClass obj)
                {
                    return new RootClassAlso();
                }
            }

            /// <summary>
            /// A class where the type is marked with LuaIgnore.
            /// </summary>
            [LuaIgnore]
            public class IgnoreOnClass : DerivedInterface1
            {
                public string Value = null;

                public object this[int i]
                {
                    set { }
                }
            }
            /// <summary>
            /// A class where the type is derived from a type that is marked
            /// with LuaIgnore.
            /// </summary>
            public class IgnoreDerived : IgnoreOnClass
            {
                public string Month = null;
            }

            /// <summary>
            /// A class where it defines a cast to 'BaseClass' however the
            /// method is marked with LuaIgnore so it shouldn't be visible.
            /// </summary>
            public class IgnoreCast
            {
                [LuaIgnore]
                public static explicit operator BaseClass(IgnoreCast obj)
                {
                    return new BaseClass();
                }
            }
            /// <summary>
            /// A class where it defines a cast to 'BaseClass' however the
            /// class is marked with LuaIgnore so no members are visible.
            /// </summary>
            [LuaIgnore]
            public class IgnoreCastAlso
            {
                public static explicit operator BaseClass(IgnoreCastAlso obj)
                {
                    return new BaseClass();
                }
            }
            /// <summary>
            /// A class where it defines a cast to 'BaseClass' however the
            /// class is marked with LuaIgnore and only the operator is given
            /// to be invisible.
            /// </summary>
            [LuaIgnore(false, IgnoreMembers = new[] { "op_Explicit" })]
            public class IgnoreCastAlso2
            {
                public static explicit operator BaseClass(IgnoreCastAlso2 obj)
                {
                    return new BaseClass();
                }
                public static implicit operator RootClass(IgnoreCastAlso2 obj)
                {
                    return new RootClass();
                }
            }
        }

        /// <summary>
        ///A test for SetIndex
        ///</summary>
        [TestMethod()]
        public void SetIndexTest()
        {
            ILuaEnvironment E = new LuaEnvironmentNet(new LuaSettings());
            LuaTable table = new LuaTable();
            table[1.0] = "1";
            table[2.0] = "2";
            table[3.0] = "3";
            table[4.0] = "4";

            // Setting numerical indicies of a table
            E.Runtime.SetIndex(E, table, 1.0, "NewValue");
            Assert.AreEqual("NewValue", table[1.0], "Setting number table index.");
            E.Runtime.SetIndex(E, table, 2.0, "NewValueAlso");
            Assert.AreEqual("NewValueAlso", table[2.0], "Setting number table index");
            Assert.AreEqual("3", table[3.0], "Setting table index.");

            // Setting object indicies of a table
            E.Runtime.SetIndex(E, table, "String", "NewValue");
            Assert.AreEqual("NewValue", table["String"], "Setting object table index.");

            // Setting table index to null
            E.Runtime.SetIndex(E, table, "String", null);
            Assert.AreEqual(null, table["String"], "Setting table index to null.");

            // Setting a missing table index
            E.Runtime.SetIndex(E, table, "Index", "Index");
            Assert.AreEqual("Index", table["Index"], "Setting a missing table index.");

            // Setting a UserData envField
            var user1 = new UserTypes.BaseClass();
            E.Runtime.SetIndex(E, user1, "Day", "Potato");
            Assert.AreEqual("Potato", user1.Day, "Setting UserData field.");
            E.Runtime.SetIndex(E, user1, "Day", null);
            Assert.AreEqual(null, user1.Day, "Setting UserData field to null.");

            // Setting a UserData property
            E.Runtime.SetIndex(E, user1, "Prop", "Temp");
            Assert.AreEqual("Temp", user1.Prop, "Setting UserData property.");
            E.Runtime.SetIndex(E, user1, "Prop", null);
            Assert.AreEqual(null, user1.Prop, "Setting UserData property to null.");

            // Setting a UserData indexer
            E.Runtime.SetIndex(E, user1, 1.0, "Temp");
            Assert.AreEqual("Temp", user1.Test, "Setting UserData indexer.");
            E.Runtime.SetIndex(E, user1, 1.0, null);
            Assert.AreEqual(null, user1.Test, "Setting UserData indexer to null.");

            // Attempt to set private envField
            try
            {
                var user2 = new UserTypes.BaseClass();
                E.Runtime.SetIndex(E, user2, "Day", 12.0);
                Assert.Fail("Setting an field to an invalid type.");
            }
            catch (InvalidCastException) { }

            // Attempt to set private envField
            try
            {
                var user2 = new UserTypes.BaseClassAlso();
                E.Runtime.SetIndex(E, user2, "hidden", "Potato");
                Assert.Fail("Setting private UserData field.");
            }
            catch (InvalidOperationException) { }

            // Attempt to set when LuaIgnore on type
            try
            {
                var user2 = new UserTypes.IgnoreOnClass();
                E.Runtime.SetIndex(E, user2, "Value", "Hidden");
                Assert.Fail("Setting field on a type marked with LuaIgnore.");
            }
            catch (InvalidOperationException) { }

            // Attempts to set when LuaIgnore inherited
            try
            {
                var user2 = new UserTypes.IgnoreDerived();
                E.Runtime.SetIndex(E, user2, "Month", "Hidden");
                Assert.Fail("Setting field on a type that inherits LuaIgnore.");
            }
            catch (InvalidOperationException) { }

            // Attempts to set a method
            try
            {
                var user2 = new UserTypes.IgnoreDerived();
                E.Runtime.SetIndex(E, user2, "Some", "Hidden");
                Assert.Fail("Setting a method.");
            }
            catch (InvalidOperationException) { }
        }

        /// <summary>
        ///A test for GetIndex
        ///</summary>
        [TestMethod()]
        public void GetIndexTest()
        {
            ILuaEnvironment E = new LuaEnvironmentNet(new LuaSettings());
            object actual;
            LuaTable table = new LuaTable();
            table[1.0] = 12;
            table[2.0] = new LuaTable();
            table[3.0] = "Potato";
            table["Item"] = "Item";

            // Get a numerical table index
            actual = E.Runtime.GetIndex(E, table, 1.0);
            Assert.AreEqual(12.0, actual, "Get a numerical table index.");

            // Get an object table index
            actual = E.Runtime.GetIndex(E, table, "Item");
            Assert.AreEqual("Item", actual, "Get an object table index.");

            // Get a missing table index
            actual = E.Runtime.GetIndex(E, table, "Missing");
            Assert.IsNull(actual, "Get a missing table index");

            // Get a UserData envField
            var user1 = new UserTypes.BaseClass();
            user1.Day = "Day";
            actual = E.Runtime.GetIndex(E, user1, "Day");
            Assert.AreEqual("Day", actual, "Get a UserData field.");

            // Get a UserData property
            user1.Prop = "Prop";
            actual = E.Runtime.GetIndex(E, user1, "Prop");
            Assert.AreEqual("Prop", actual, "Get a UserData property.");

            // Get a UserData indexer
            actual = E.Runtime.GetIndex(E, user1, 10.0);
            Assert.AreEqual("", actual, "Get a UserData indexer.");

            // Get a UserData method
            actual = E.Runtime.GetIndex(E, user1, "Some");
            Assert.IsInstanceOfType(actual, typeof(IMethod), "Get a UserData method.");

            // Attempt to get a private envField
            try
            {
                var user2 = new UserTypes.BaseClassAlso();
                actual = E.Runtime.GetIndex(E, user2, "hidden");
                Assert.Fail("Attempt to get a private field.");
            }
            catch (InvalidOperationException) { }

            // Attempt to get where type marked with LuaIgnore
            try
            {
                var user2 = new UserTypes.IgnoreOnClass();
                actual = E.Runtime.GetIndex(E, user2, "Value");
                Assert.Fail("Attempt to get where type marked with LuaIgnore.");
            }
            catch (InvalidOperationException) { }

            // Attempt to get where type derives a LuaIgnore
            try
            {
                var user2 = new UserTypes.IgnoreDerived();
                actual = E.Runtime.GetIndex(E, user2, "Month");
                Assert.Fail("Attempt to get where type derives LuaIgnore.");
            }
            catch (InvalidOperationException) { }
        }

        /// <summary>
        ///A test for GetCompatibleMethod
        ///</summary>
        [TestMethod()]
        public void GetCompatibleMethodTest()
        {
            var E = new LuaEnvironmentNet(new LuaSettings());
            var fooDerived = typeof(UserTypes.DerivedClass).GetMethods().Where(m => m.Name == "Foo").ToArray();
            var fooRoot = typeof(UserTypes.RootClass).GetMethods().Where(m => m.Name == "Foo").ToArray();
            var foo2Derived = typeof(UserTypes.DerivedClass).GetMethods().Where(m => m.Name == "Foo2").ToArray();
            var foo3Derived = typeof(UserTypes.DerivedClass).GetMethods().Where(m => m.Name == "Foo3").ToArray();
            var targetsDerived = new[] { new UserTypes.DerivedClass() };
            var targetsRoot = new[] { new UserTypes.RootClass() };
            object[] args;
            int[] byref;
            MethodInfo method;
            object target;
            bool actual;

            // no arguments with no conflicts
            args = new object[0];
            byref = new int[0];
            actual = NetHelpers.GetCompatibleMethod(fooDerived, targetsDerived, ref args, byref, out method, out target);
            Assert.AreEqual(true, actual, "Zero arguments with no conflicts.");
            Assert.AreEqual(typeof(UserTypes.DerivedClass).GetMethod("Foo", new Type[0]),
                method, "Zero arguments with no conflicts");

            // one argument where options are 'string' and 'object'
            //   should chose the one with more specific arguments
            args = new[] { "Foo" };
            byref = new int[0];
            actual = NetHelpers.GetCompatibleMethod(fooDerived, targetsDerived, ref args, byref, out method, out target);
            Assert.AreEqual(true, actual, "Inheritence conflict.");
            Assert.AreEqual(typeof(UserTypes.DerivedClass).GetMethod("Foo", new Type[] { typeof(string) }),
                method, "Inheritence conflict.");

            // one argument with conflict
            try
            {
                args = new object[] { null };
                byref = new int[0];
                actual = NetHelpers.GetCompatibleMethod(fooDerived, targetsDerived, ref args, byref,
                    out method, out target);
                Assert.Fail("Conflict with null.");
            }
            catch (AmbiguousMatchException) { }

            // user-defined type
            args = new[] { new UserTypes.DerivedClass() };
            byref = new int[0];
            actual = NetHelpers.GetCompatibleMethod(fooDerived, targetsDerived, ref args, byref, out method, out target);
            Assert.AreEqual(true, actual, "User-defined type.");
            Assert.AreEqual(typeof(UserTypes.DerivedClass).GetMethod("Foo", new Type[] { typeof(UserTypes.DerivedClass) }),
                method, "User-defined type.");

            // user-defined cast
            args = new object[] { new UserTypes.DerivedClassAlso(), 12 };
            byref = new int[0];
            actual = NetHelpers.GetCompatibleMethod(fooDerived, targetsDerived, ref args, byref, out method, out target);
            Assert.AreEqual(true, actual, "User-defined cast.");
            Assert.AreEqual(typeof(UserTypes.DerivedClass).GetMethod("Foo", new Type[] { typeof(UserTypes.DerivedClass), typeof(int) }),
                method, "User-defined cast.");
            Assert.IsInstanceOfType(args[0], typeof(UserTypes.DerivedClass), "One argument with user-defined cast.");

            // derived type markes with LuaIgnore
            args = new object[] { 12.0 };
            byref = new int[0];
            actual = NetHelpers.GetCompatibleMethod(fooDerived, targetsRoot, ref args, byref, out method, out target);
            Assert.AreEqual(true, actual, "Derived type marked with LuaIgnore.");
            Assert.AreEqual(typeof(UserTypes.DerivedClass).GetMethod("Foo", new Type[] { typeof(object) }),
                method, "Derived type marked with LuaIgnore.");

            // empty parameter array
            args = new[] { "Pie", "Pie" };
            byref = new int[0];
            actual = NetHelpers.GetCompatibleMethod(fooDerived, targetsDerived, ref args, byref, out method, out target);
            Assert.AreEqual(true, actual, "Empty parameter array.");
            Assert.AreEqual(typeof(UserTypes.DerivedClass).GetMethod("Foo", new Type[] { typeof(object), typeof(object) }),
                method, "Empty parameter array.");

            // two arguments with conflict with method marked with LuaIgnore
            args = new object[] { "Pie", 12.0 };
            byref = new int[0];
            actual = NetHelpers.GetCompatibleMethod(fooDerived, targetsDerived, ref args, byref, out method, out target);
            Assert.AreEqual(true, actual, "Conflict with method marked with LuaIgnore.");
            Assert.AreEqual(typeof(UserTypes.DerivedClass).GetMethod("Foo", new Type[] { typeof(string), typeof(int) }),
                method, "Conflict with method marked with LuaIgnore.");

            // conflict between params arrays,
            //   should pick the one with more specific params type
            args = new object[] { 12.0, 12.0, "Foo", "Foo" };
            byref = new int[0];
            actual = NetHelpers.GetCompatibleMethod(fooDerived, targetsDerived, ref args, byref, out method, out target);
            Assert.AreEqual(true, actual, "Conflict with params array type.");
            Assert.AreEqual(typeof(UserTypes.DerivedClass).GetMethod("Foo", new Type[] { typeof(int), typeof(int), typeof(string[]) }),
                method, "Conflict with params array type.");

            // three arguments with conflict between explicit parameters,
            //   should pick the one with more explicit parameters
            //   Foo(object, object, string, params string[])
            args = new object[] { "Foo", "Foo", "Foo", "Foo", "Foo" };
            byref = new int[0];
            actual = NetHelpers.GetCompatibleMethod(fooDerived, targetsDerived, ref args, byref, out method, out target);
            Assert.AreEqual(true, actual, "Conflict between params array and explicit parameters.");
            Assert.AreEqual(typeof(UserTypes.DerivedClass).GetMethod("Foo",
                new Type[] { typeof(object), typeof(object), typeof(string), typeof(string[]) }),
                method, "Conflict between params array and explicit parameters.");

            // three arguments with conflict between optional parameters and params array
            args = new object[] { "Foo", "Foo", 12.0 };
            byref = new int[0];
            actual = NetHelpers.GetCompatibleMethod(fooDerived, targetsDerived, ref args, byref, out method, out target);
            Assert.AreEqual(true, actual, "Conflict between optional parameters and params array.");
            Assert.AreEqual(typeof(UserTypes.DerivedClass).GetMethod("Foo",
                new Type[] { typeof(object), typeof(object), typeof(int), typeof(int), typeof(int) }),
                method, "Conflict between optional parameters and params array.");

            // one argument not passed by reference
            args = new object[] { "Foo" };
            byref = new int[0];
            actual = NetHelpers.GetCompatibleMethod(foo2Derived, targetsDerived, ref args, byref, out method, out target);
            Assert.AreEqual(true, actual, "With overload that is pass by-reference.");
            Assert.AreEqual(typeof(UserTypes.DerivedClass).GetMethod("Foo2",
                new Type[] { typeof(object) }),
                method, "With overload that is pass by-reference.");

            // one argument passed by reference
            args = new object[] { "Foo" };
            byref = new[] { 0 };
            actual = NetHelpers.GetCompatibleMethod(foo2Derived, targetsDerived, ref args, byref, out method, out target);
            Assert.AreEqual(true, actual, "Argument passed by-reference.");
            Assert.AreEqual(typeof(UserTypes.DerivedClass).GetMethod("Foo2",
                new Type[] { typeof(object).MakeByRefType() }),
                method, "Argument passed by-reference.");

            // two arguments not passed by reference
            args = new object[] { "Foo", "Foo" };
            byref = new int[0];
            actual = NetHelpers.GetCompatibleMethod(foo2Derived, targetsDerived, ref args, byref, out method, out target);
            Assert.AreEqual(false, actual, "Normal variable not compatible with pass by-reference.");

            // two arguments passed by reference
            args = new object[] { "Foo", "Foo" };
            byref = new[] { 1 };
            actual = NetHelpers.GetCompatibleMethod(foo2Derived, targetsDerived, ref args, byref, out method, out target);
            Assert.AreEqual(true, actual, "Conflict with by-reference inheritence.");
            Assert.AreEqual(typeof(UserTypes.DerivedClass).GetMethod("Foo2",
                new Type[] { typeof(object), typeof(string).MakeByRefType() }),
                method, "Conflict with by-reference inheritence.");

            // two arguments passed by reference with ambiguity
            try
            {
                args = new object[] { "Foo", null };
                byref = new[] { 1 };
                actual = NetHelpers.GetCompatibleMethod(foo2Derived, targetsDerived, ref args, byref, out method, out target);
                Assert.Fail("Pass by-reference with ambiguity.");
            }
            catch (AmbiguousMatchException) { }

            // conflict between LuaUserData and normal type
            args = new object[] { "Foo" };
            byref = new int[0];
            actual = NetHelpers.GetCompatibleMethod(foo3Derived, targetsDerived, ref args, byref, out method, out target);
            Assert.AreEqual(true, actual, "Conflict between LuaUserData and normal type.");
            Assert.AreEqual(typeof(UserTypes.DerivedClass).GetMethod("Foo3",
                new Type[] { typeof(LuaUserData<string>) }),
                method, "Conflict between LuaUserData and normal type.");

            // conflict between LuaUserData types
            args = new object[] { 12.0, "Foo" };
            byref = new int[0];
            actual = NetHelpers.GetCompatibleMethod(foo3Derived, targetsDerived, ref args, byref, out method, out target);
            Assert.AreEqual(true, actual, "Conflict between LuaUserData types.");
            Assert.AreEqual(typeof(UserTypes.DerivedClass).GetMethod("Foo3",
                new Type[] { typeof(int), typeof(LuaUserData<string>) }),
                method, "Conflict between LuaUserData types.");
        }

        /// <summary>
        ///A test for ConvertType
        ///</summary>
        [TestMethod()]
        public void ConvertTypeTest()
        {
            LuaRuntimeNet target = LuaRuntimeNet.Create(null);
            object actual;

            // convert between numbers
            actual = target.ConvertType((short)123, typeof(long));
            Assert.IsNotNull(actual, "Numerical conversion: 'short' to 'long'");
            Assert.IsInstanceOfType(actual, typeof(long), "Numerical conversion: 'short' to 'long'");
            actual = target.ConvertType(34.2345, typeof(sbyte));
            Assert.IsNotNull(actual, "Numerical conversion: 'double' to 'sbyte'");
            Assert.IsInstanceOfType(actual, typeof(sbyte), "Numerical conversion: 'double' to 'sbyte'");

            // convert between implicit casts
            actual = target.ConvertType("pie", typeof(IEnumerable));
            Assert.IsNotNull(actual, "Implements an interface: 'string' to 'IEnumerable'");
            Assert.IsInstanceOfType(actual, typeof(IEnumerable), "Implements an interface: 'string' to 'IEnumerable'");
            actual = target.ConvertType(new StringBuilder(), typeof(StringBuilder));
            Assert.IsNotNull(actual, "The same type: 'StringBuilder' and 'StringBuilder'");
            Assert.IsInstanceOfType(actual, typeof(StringBuilder), "The same type: 'StringBuilder' and 'StringBuilder'");

            // convert between derived user-defined types
            actual = target.ConvertType(new UserTypes.BaseClass(), typeof(UserTypes.BaseInterface));
            Assert.IsNotNull(actual, "User-defined implement interface: 'BaseClass' to 'BaseInterface'");
            Assert.IsInstanceOfType(actual, typeof(UserTypes.BaseInterface),
                "User-defined implement interface: 'BaseClass' to 'BaseInterface'");
            actual = target.ConvertType(new UserTypes.RootClassAlso(), typeof(UserTypes.BaseClassAlso));
            Assert.IsNotNull(actual, "User-defined derived class: 'RootClassAlso' to 'BaseClassAlso'");
            Assert.IsInstanceOfType(actual, typeof(UserTypes.BaseClassAlso),
                "User-defined derived class: 'RootClassAlso' to 'BaseClassAlso'");

            // convert using user-defined 'explicit' cast
            actual = target.ConvertType(new UserTypes.DerivedClassAlso(), typeof(UserTypes.DerivedClass));
            Assert.IsNotNull(actual, "User-defined explicit cast in underlying type: 'DerivedClassAlso' to 'DerivedClass'");
            Assert.IsInstanceOfType(actual, typeof(UserTypes.DerivedClass),
                "User-defined explicit cast in underlying type: 'DerivedClassAlso' to 'DerivedClass'");

            // convert using user-defined 'explicit' cast in other type
            actual = target.ConvertType(new UserTypes.DerivedClass(), typeof(UserTypes.DerivedClassAlso));
            Assert.IsNotNull(actual, "User-defined explicit cast in destination type: 'DerivedClass' to 'DerivedClassAlso'");
            Assert.IsInstanceOfType(actual, typeof(UserTypes.DerivedClassAlso),
                "User-defined explicit cast in destination type: 'DerivedClass' to 'DerivedClassAlso'");

            // convert using user-defined 'implicit' cast
            actual = target.ConvertType(new UserTypes.RootClassAlso(), typeof(UserTypes.RootClass));
            Assert.IsNotNull(actual, "User-defined implicit cast in underlying type: 'RootClassAlso' to 'RootClass'");
            Assert.IsInstanceOfType(actual, typeof(UserTypes.RootClass),
                "User-defined implicit cast in underlying type: 'RootClassAlso' to 'RootClass'");

            // convert using user-defined 'implicit' cast in other type
            actual = target.ConvertType(new UserTypes.RootClass(), typeof(UserTypes.RootClassAlso));
            Assert.IsNotNull(actual, "User-defined implicit cast in destination type: 'RootClass' to 'RootClassAlso'");
            Assert.IsInstanceOfType(actual, typeof(UserTypes.RootClassAlso),
                "User-defined implicit cast in destination type: 'RootClass' to 'RootClassAlso'");

            // convert from LuaUserData to backing type
            actual = target.ConvertType(new LuaUserData(new UserTypes.RootClass()), typeof(UserTypes.RootClass));
            Assert.IsNotNull(actual, "LuaUserData to type of backing object: 'LuaUserData' to 'RootClass'");
            Assert.IsInstanceOfType(actual, typeof(UserTypes.RootClass),
                "LuaUserData to type of backing object: 'LuaUserData' to 'RootClass'");

            // convert from LuaUserData using user-defined 'implicit' cast
            actual = target.ConvertType(new LuaUserData(new UserTypes.RootClass()), typeof(UserTypes.RootClassAlso));
            Assert.IsNotNull(actual, "LuaUserData using user-defined implicit cast: 'LuaUserData' to 'RootClassAlso'");
            Assert.IsInstanceOfType(actual, typeof(UserTypes.RootClassAlso),
                "LuaUserData using user-defined implicit cast: 'LuaUserData' to 'RootClassAlso'");

            // convert LuaUserData to generic version
            actual = target.ConvertType(new LuaUserData(new UserTypes.RootClass()), typeof(LuaUserData<UserTypes.RootClass>));
            Assert.IsNotNull(actual, "LuaUserData to generic version: 'LuaUserData' to 'LuaUserData<RootClass>'");
            Assert.IsInstanceOfType(actual, typeof(LuaUserData<UserTypes.RootClass>),
                "LuaUserData to generic version: 'LuaUserData' to 'LuaUserData<RootClass>'");

            // convert object to generic LuaUserData
            actual = target.ConvertType(new UserTypes.RootClass(), typeof(LuaUserData<UserTypes.RootClass>));
            Assert.IsNotNull(actual, "Normal object to generic LusUserData: 'RootClass' to 'LuaUserData<RootClass>'");
            Assert.IsInstanceOfType(actual, typeof(LuaUserData<UserTypes.RootClass>),
                "Normal object to generic LusUserData: 'RootClass' to 'LuaUserData<RootClass>'");

            // error converting between types
            try
            {
                actual = target.ConvertType(new StringBuilder(), typeof(Stream));
                Assert.Fail("Incompatible types");
            }
            catch (InvalidCastException) { }

            // error converting between user-defined types
            try
            {
                actual = target.ConvertType(new UserTypes.BaseClass(), typeof(UserTypes.DerivedInterface1));
                Assert.Fail("User-defined type does not implement interface");
            }
            catch (InvalidCastException) { }

            // error converting in the opposite direction of inheritence hierarchy
            try
            {
                actual = target.ConvertType(new UserTypes.BaseClass(), typeof(UserTypes.DerivedClass));
                Assert.Fail("Converting to a derived type");
            }
            catch (InvalidCastException) { }

            // error converting when the operator is marked with LuaIgnore.
            try
            {
                actual = target.ConvertType(new UserTypes.IgnoreCast(), typeof(UserTypes.BaseClass));
                Assert.Fail("The operator is marked with LuaIgnore");
            }
            catch (InvalidCastException) { }

            // error converting when the type is marked with LuaIgnore.
            try
            {
                actual = target.ConvertType(new UserTypes.IgnoreCastAlso(), typeof(UserTypes.BaseClass));
                Assert.Fail("The underlying type is marked with LuaIgnore");
            }
            catch (InvalidCastException) { }

            // error converting when the type is marked with LuaIgnore and only
            //   the operator is not visible
            try
            {
                actual = target.ConvertType(new UserTypes.IgnoreCastAlso2(), typeof(UserTypes.BaseClass));
                Assert.Fail("Underlying type is marked with LuaIgnore and has operator as IgnoreMembers'");
            }
            catch (InvalidCastException) { }

            // error converting between LuaUserData types
            try
            {
                actual = target.ConvertType(new LuaUserData(12), typeof(LuaUserData<string>));
                Assert.Fail("From LuaUserData to an invalid LuaUserData type.");
            }
            catch (InvalidCastException) { }

            // convert using user-defined 'implicit' cast where the explicit
            //   operator is not visible
            actual = target.ConvertType(new UserTypes.IgnoreCastAlso2(), typeof(UserTypes.RootClass));
            Assert.IsNotNull(actual, "Underlying type is marked with LuaIgnore but this operator is visible");
            Assert.IsInstanceOfType(actual, typeof(UserTypes.RootClass), "Underlying type is marked with LuaIgnore but this operator is visible");
        }
    }*/
}
