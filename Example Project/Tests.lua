-- Copyright 2014 Jacob Trimble
--
-- Licensed under the Apache License, Version 2.0 (the "License");
-- you may not use this file except in compliance with the License.
-- You may obtain a copy of the License at
--
--     http://www.apache.org/licenses/LICENSE-2.0
--
-- Unless required by applicable law or agreed to in writing, software
-- distributed under the License is distributed on an "AS IS" BASIS,
-- WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
-- See the License for the specific language governing permissions and
-- limitations under the License.

-- A general test for some the features of Lua and ModMaker.Lua.

do
	io.write("General assignment...\t")

	-- create a table and alter it's contents
	local t = {}
	t[1] = "Foo"
	t[2] = "Foo2"
	assert(#{} == 0, "Table length comparison.")
	assert(#t == 2, "Table length comparison.")
	assert((t[1] == "Foo") and (t[2] == "Foo2"), "Table get/set values.")

	-- assignment order of evaluation
	local i = 3
	i, t[i] = i+1, 20
	assert((t[3] == 20) and(t[4] == nil), "Assignment order of evaluation.")

	io.write("Pass\n")
end

do
	io.write("Advanced expressions...\t")

	-- temporary function
	local function temp(a) end

	-- swap
	local a, b = 1, 2
	a, b = b, a
	assert((a == 2) and (b == 1), "Variable swap.")

	-- 
	local c = a + b; (temp or print)('done')

	-- and/or and expressions
	local x = a + c / 3
	x = x ^ 2
	local ddd = nil and assert(false)
	local s22 = 12 or assert(false)
	local x2 = (nil or x) and 12
	assert(x == 9, "Numerical expressions")
	assert(x2 == 12, "And/or of values")
	assert((nil or 12) == 12, "And/or of values")
	assert((nil and 12) == nil, "And/or of values")
	assert((12 or nil) == 12, "And/or of values")
	assert((12 and nil) == nil, "And/or of values")

	io.write("Pass\n")
end

-- numerical for loop
do
	io.write("For loop...\t\t")

	local i = 0
	for j = 0, 10, 2 do
		i = i + j
	end
	assert(i == 30, "Numerical for loop.")

	io.write("Pass\n")
end

-- generic for loop
do
	io.write("Generic for loop...\t")

	local t = {[1]="One", [2] = "Two", [3]= "Three" }
	for k, v in pairs(t) do
		if k == 1 then
			assert(v == "One", "Generic for loop.")
		elseif k == 2 then
			assert(v == "Two", "Generic for loop.")
		elseif k == 3 then
			assert(v == "Three", "Generic for loop.")
		else
			assert(false, "Generic for loop.")
		end
	end

	io.write("Pass\n")
end

-- coroutines
if DoThreads then
	io.write("Coroutines...\t")

	local function foo (a)
		assert(a == 2, "Coroutines pass to nested function")
		return coroutine.yield(2*a)
    end

    co = coroutine.create(function (a,b)
		assert((a == 1) and (b == 10), "Coroutines passes from create")
		local r = foo(a+1)
		assert(r == "r", "Coroutines returns from yield, nested")

		local r, s = coroutine.yield(a+b, a-b)
		assert((r == "x") and (s == "y"), "Coroutines returns from yield")

		return b, "end"
    end)
     
	local a, b, c = coroutine.resume(co, 1, 10);
	assert(a and (b == 4), "Coroutines returns from resume")
	a, b, c = coroutine.resume(co, "r")
	assert(a and (b == 11) and (c == -9), "Coroutines returns from resume")
	a, b, c = coroutine.resume(co, "x", "y")
	assert(a and (b == 10) and (c == "end"), "Coroutines returns from resume at end")
	a, b, c = coroutine.resume(co, "x", "y")
	assert(not a and (b == "cannot resume dead coroutine"), "Coroutines fails for dead thread")

	io.write("\tPass\n")
end

--[[ recusion and proper tail-calls
if TailCalls then
	io.write("Proper tail calls...\t")

	local function recurse(i)
		if i > 100000 then
			return i
		else
			return recurse(i + 1)
		end
	end

	recurse(1)

	io.write("Pass\n")
end]]

-- traditional OOP
do
	io.write("Traditional OOP...\t")

	local Account = {balance = 0}

	function Account:new (o, name)
		o = o or {name=name}
		setmetatable(o, self)
		self.__index = self
		return o
	end

	function Account:deposit (v)
		self.balance = self.balance + v
	end

	function Account:withdraw (v)
		assert(self.balance > v, "Traditional OOP")
		self.balance = self.balance - v
	end
	
	local a = Account:new(nil,"demo")
	a:deposit(1000.00)
	assert(a.balance == 1000, "Traditional OOP")
	a:withdraw(100)
	assert(a.balance == 900, "Traditional OOP")

	io.write("Pass\n")
end

-- by-reference
do
	io.write("By-reference...\t\t")

	i = 12
	Foo(@i)
	assert(i == 24, "By-Reference variables");

	t = { 12 }
	Foo(ref t[1])
	assert(t[1] == 24, "By-Reference variables");

	function Temp(i)
		i = 12
	end

	Temp(ref(i))
	assert(i == 24, "By-Reference variables");

	io.write("Pass\n")
end

-- lua defined type
do
	io.write("Lua defined type...\t")

	class MyLuaClass : ITest

	function MyLuaClass:Do()
		return true
	end

	function MyLuaClass:Some()
		return 12
	end

	function MyLuaClass:Test()
	  return 'foobar'
    end
	
	MyLuaClass.Field1 = 12
	MyLuaClass.Field2 = { out = 234 }

	local temp = MyLuaClass()
	assert(temp, "Lua defined type")
	assert(temp.Do() == true, "MyLuaClass::Do should return true")
	assert(temp:Some() == 12, "MyLuaClass::Some should return 12")
	assert(temp:Test() == 'foobar', "MyLuaClass::Test should return 'foobar'")
	assert(temp.Field1 == 12, "MyLuaClass::Field1 should be 12")
	assert(type(temp.Field2) == "table", "MyLuaClass::Field2 should be a table")
	assert(temp.Field2.out == 234, "MyLuaClass::Field2.out should be 234")

	io.write("Pass\n")
end

print("\nAll tests succeeded.")