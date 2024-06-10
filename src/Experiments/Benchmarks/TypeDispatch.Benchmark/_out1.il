# F#  .NET: 8.0.2 
# FsharpTypeDispatch+TypeDispatch Memory Layout.  
class FsharpTypeDispatch+TypeDispatch
{
    Metadata:
    ┌──────────────────────────────────────────┐
    │ [0-7]     Object Header       (8 bytes)  │
    │ [8-15]    Method Table Ptr    (8 bytes)  │
    └──────────────────────────────────────────┘
    Fields:
      [16-23]   Padding             (8 bytes)
    Size:    24 
    Padding: 8 
}



# Instruction Count: 37; Code Size: 112
TypeDispatch+Boolean IsNumericType(Type):
              0000: push   rsi                # stack.push(rsi)
              0001: push   rbx                # stack.push(rbx)
              0002: sub    rsp, 28h           # stack.push_times(5)
              0006: mov    rbx, rcx           # rbx = rcx
              0009: test   rbx, rbx           # if(rbx & rbx == 0)
      ┌──────◦000C: je     short 0032 ⇣       # goto 32
      │       000E: mov    rcx FFFFFFFFFF85A2D8 ↷, 7FF9E24CA318h FFFFFFFFFF85A2D8 ↷ # rcx = typeof(System.RuntimeType)
      │       0018: cmp    [rbx], rcx         # if(Memory[rbx] != rcx)
    ┌─┼──────◦001B: jne    short 005C ⇣       # └> goto 5C
    │ │       001D: mov    rcx, rbx           # rcx = rbx
    │ │       0020: call   qword ptr [7FF9E24CA6E0h] # qword ptr [7FF9E24CA6E0h]
    │ │       0026: mov    esi, eax           # esi = eax
  ┌─┼─┼───┌──>0028: add    esi, 0FFFFFFFBh    # esi += 4294967291
  │ │ │   │   002B: cmp    esi, 9             # if(esi > 9)
  │ │ │ ┌─┼──◦002E: ja     short 0042 ⇣       # └> goto 42
  │ │ │ │ │ ┌◦0030: jmp    short 0036 ⇣       # goto 36
  │ │ └─┼─┼─┼>0032: xor    esi, esi           # esi = 0
  │ │   │ └─┼◦0034: jmp    short 0028 ⇡       # goto 28
  │ │   │   └>0036: mov    eax, 1             # eax = 1
  │ │   │     003B: add    rsp, 28h           # stack.pop_times(5)
  │ │   │     003F: pop    rbx                # rbx = stack.pop()
  │ │   │     0040: pop    rsi                # rsi = stack.pop()
  │ │   │     0041: ret                       # return;
  │ │   └────>0042: mov    rax, 2520021ADA8h  # rax = 2551212780968
  │ │         004C: cmp    rbx, rax           # if(rbx NA rax)
  │ │         004F: sete   al                
  │ │         0052: movzx  eax, al            # (32bit)eax = (16bit)al (zero extend)
  │ │         0055: add    rsp, 28h           # stack.pop_times(5)
  │ │         0059: pop    rbx                # rbx = stack.pop()
  │ │         005A: pop    rsi                # rsi = stack.pop()
  │ │         005B: ret                       # return;
  │ └────────>005C: mov    rcx, rbx           # rcx = rbx
  │           005F: mov    rax, [rbx]         # rax = Memory[rbx]
  │           0062: mov    rax, [rax+98h]     # rax = Memory[rax+152]
  │           0069: call   qword ptr [rax+10h] # qword ptr [rax+10h]
  │           006C: mov    esi, eax           # esi = eax
  └──────────◦006E: jmp    short 0028 ⇡       # goto 28
# Instruction Count: 36; Code Size: 120
TypeDispatch+Boolean IsNumericType2(Type):
              0000: push   rsi                # stack.push(rsi)
              0001: push   rbx                # stack.push(rbx)
              0002: sub    rsp, 28h           # stack.push_times(5)
              0006: mov    rbx, rcx           # rbx = rcx
              0009: test   rbx, rbx           # if(rbx & rbx == 0)
      ┌──────◦000C: je     short 0032 ⇣       # goto 32
      │       000E: mov    rcx FFFFFFFFFF85A248 ↷, 7FF9E24CA318h FFFFFFFFFF85A248 ↷ # rcx = typeof(System.RuntimeType)
      │       0018: cmp    [rbx], rcx         # if(Memory[rbx] != rcx)
    ┌─┼──────◦001B: jne    short 0064 ⇣       # └> goto 64
    │ │       001D: mov    rcx, rbx           # rcx = rbx
    │ │       0020: call   qword ptr [7FF9E24CA6E0h] # qword ptr [7FF9E24CA6E0h]
    │ │       0026: mov    esi, eax           # esi = eax
  ┌─┼─┼───┌──>0028: add    esi, 0FFFFFFFBh    # esi += 4294967291
  │ │ │   │   002B: cmp    esi, 9             # if(esi > 9)
  │ │ │ ┌─┼──◦002E: ja     short 0042 ⇣       # └> goto 42
  │ │ │ │ │ ┌◦0030: jmp    short 0036 ⇣       # goto 36
  │ │ └─┼─┼─┼>0032: xor    esi, esi           # esi = 0
  │ │   │ └─┼◦0034: jmp    short 0028 ⇡       # goto 28
  │ │   │   └>0036: mov    eax, 1             # eax = 1
  │ │   │     003B: add    rsp, 28h           # stack.pop_times(5)
  │ │   │     003F: pop    rbx                # rbx = stack.pop()
  │ │   │     0040: pop    rsi                # rsi = stack.pop()
  │ │   │     0041: ret                       # return;
  │ │   └────>0042: mov    rdx, rbx           # rdx = rbx
  │ │         0045: mov    rcx 9E8D70 ↷, 7FF9E3658E40h 9E8D70 ↷ # Microsoft.FSharp.Core.LanguagePrimitives+HashCompare.GenericEqualityIntrinsic[[System.Type, System.Private.CoreLib]](System.Type, System.Type) = Microsoft.FSharp.Core.LanguagePrimitives+HashCompare.GenericEqualityIntrinsic[[System.Type, System.Private.CoreLib]](System.Type, System.Type)
  │ │         004F: mov    r8, 2520021ADA8h   # r8 = 2551212780968
  │ │         0059: add    rsp, 28h           # stack.pop_times(5)
  │ │         005D: pop    rbx                # rbx = stack.pop()
  │ │         005E: pop    rsi                # rsi = stack.pop()
  │ │         005F: jmp    near ptr 00007FF9E2CA06C0h 305F0 ↷ # goto 305F0
  │ └────────>0064: mov    rcx, rbx           # rcx = rbx
  │           0067: mov    rax, [rbx]         # rax = Memory[rbx]
  │           006A: mov    rax, [rax+98h]     # rax = Memory[rax+152]
  │           0071: call   qword ptr [rax+10h] # qword ptr [rax+10h]
  │           0074: mov    esi, eax           # esi = eax
  └──────────◦0076: jmp    short 0028 ⇡       # goto 28
# Instruction Count: 41; Code Size: 112
TypeDispatch+Boolean IsNumericType(Type):
              0000: push   rsi                # stack.push(rsi)
              0001: push   rbx                # stack.push(rbx)
              0002: sub    rsp, 28h           # stack.push_times(5)
              0006: mov    rbx, rcx           # rbx = rcx
              0009: test   rbx, rbx           # if(rbx & rbx == 0)
        ┌────◦000C: je     short 0032 ⇣       # goto 32
        │     000E: mov    rcx FFFFFFFFFF85A2D8 ↷, 7FF9E24CA318h FFFFFFFFFF85A2D8 ↷ # rcx = typeof(System.RuntimeType)
        │     0018: cmp    [rbx], rcx         # if(Memory[rbx] != rcx)
    ┌───┼────◦001B: jne    short 005C ⇣       # └> goto 5C
    │   │     001D: mov    rcx, rbx           # rcx = rbx
    │   │     0020: call   qword ptr [7FF9E24CA6E0h] # qword ptr [7FF9E24CA6E0h]
    │   │     0026: mov    esi, eax           # esi = eax
  ┌─┼───┼───┌>0028: add    esi, 0FFFFFFFBh    # esi += 4294967291
  │ │   │   │ 002B: cmp    esi, 9             # if(esi > 9)
  │ │ ┌─┼───┼◦002E: ja     short 0042 ⇣       # └> goto 42
  │ │ │ │ ┌─┼◦0030: jmp    short 0036 ⇣       # goto 36
  │ │ │ └─┼─┼>0032: xor    esi, esi           # esi = 0
  │ │ │   │ └◦0034: jmp    short 0028 ⇡       # goto 28
  │ │ │   └──>0036: mov    eax, 1             # eax = 1
  │ │ │       003B: add    rsp, 28h           # stack.pop_times(5)
  │ │ │       003F: pop    rbx                # rbx = stack.pop()
  │ │ │       0040: pop    rsi                # rsi = stack.pop()
  │ │ │       0041: ret                       # return;
  │ │ └──────>0042: mov    rax, 2520021ADA8h  # rax = 2551212780968
  │ │         004C: cmp    rbx, rax           # if(rbx NA rax)
  │ │         004F: sete   al                
  │ │         0052: movzx  eax, al            # (32bit)eax = (16bit)al (zero extend)
  │ │         0055: add    rsp, 28h           # stack.pop_times(5)
  │ │         0059: pop    rbx                # rbx = stack.pop()
  │ │         005A: pop    rsi                # rsi = stack.pop()
  │ │         005B: ret                       # return;
  │ └────────>005C: mov    rcx, rbx           # rcx = rbx
  │           005F: mov    rax, [rbx]         # rax = Memory[rbx]
  │           0062: mov    rax, [rax+98h]     # rax = Memory[rax+152]
  │           0069: call   qword ptr [rax+10h] # qword ptr [rax+10h]
  │           006C: mov    esi, eax           # esi = eax
  └──────────◦006E: jmp    short 0028 ⇡       # goto 28
# Instruction Count: 40; Code Size: 120
TypeDispatch+Boolean IsNumericType2(Type):
              0000: push   rsi                # stack.push(rsi)
              0001: push   rbx                # stack.push(rbx)
              0002: sub    rsp, 28h           # stack.push_times(5)
              0006: mov    rbx, rcx           # rbx = rcx
              0009: test   rbx, rbx           # if(rbx & rbx == 0)
        ┌────◦000C: je     short 0032 ⇣       # goto 32
        │     000E: mov    rcx FFFFFFFFFF85A248 ↷, 7FF9E24CA318h FFFFFFFFFF85A248 ↷ # rcx = typeof(System.RuntimeType)
        │     0018: cmp    [rbx], rcx         # if(Memory[rbx] != rcx)
    ┌───┼────◦001B: jne    short 0064 ⇣       # └> goto 64
    │   │     001D: mov    rcx, rbx           # rcx = rbx
    │   │     0020: call   qword ptr [7FF9E24CA6E0h] # qword ptr [7FF9E24CA6E0h]
    │   │     0026: mov    esi, eax           # esi = eax
  ┌─┼───┼───┌>0028: add    esi, 0FFFFFFFBh    # esi += 4294967291
  │ │   │   │ 002B: cmp    esi, 9             # if(esi > 9)
  │ │ ┌─┼───┼◦002E: ja     short 0042 ⇣       # └> goto 42
  │ │ │ │ ┌─┼◦0030: jmp    short 0036 ⇣       # goto 36
  │ │ │ └─┼─┼>0032: xor    esi, esi           # esi = 0
  │ │ │   │ └◦0034: jmp    short 0028 ⇡       # goto 28
  │ │ │   └──>0036: mov    eax, 1             # eax = 1
  │ │ │       003B: add    rsp, 28h           # stack.pop_times(5)
  │ │ │       003F: pop    rbx                # rbx = stack.pop()
  │ │ │       0040: pop    rsi                # rsi = stack.pop()
  │ │ │       0041: ret                       # return;
  │ │ └──────>0042: mov    rdx, rbx           # rdx = rbx
  │ │         0045: mov    rcx 9E8D70 ↷, 7FF9E3658E40h 9E8D70 ↷ # Microsoft.FSharp.Core.LanguagePrimitives+HashCompare.GenericEqualityIntrinsic[[System.Type, System.Private.CoreLib]](System.Type, System.Type) = Microsoft.FSharp.Core.LanguagePrimitives+HashCompare.GenericEqualityIntrinsic[[System.Type, System.Private.CoreLib]](System.Type, System.Type)
  │ │         004F: mov    r8, 2520021ADA8h   # r8 = 2551212780968
  │ │         0059: add    rsp, 28h           # stack.pop_times(5)
  │ │         005D: pop    rbx                # rbx = stack.pop()
  │ │         005E: pop    rsi                # rsi = stack.pop()
  │ │         005F: jmp    near ptr 00007FF9E2CA06C0h 305F0 ↷ # goto 305F0
  │ └────────>0064: mov    rcx, rbx           # rcx = rbx
  │           0067: mov    rax, [rbx]         # rax = Memory[rbx]
  │           006A: mov    rax, [rax+98h]     # rax = Memory[rax+152]
  │           0071: call   qword ptr [rax+10h] # qword ptr [rax+10h]
  │           0074: mov    esi, eax           # esi = eax
  └──────────◦0076: jmp    short 0028 ⇡       # goto 28

