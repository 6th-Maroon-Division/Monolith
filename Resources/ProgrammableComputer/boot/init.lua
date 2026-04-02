-- Lua Computer default boot script
-- Key code constants (Robust.Client.Input.Keyboard.Key cast to int)
local K = {
  A=10,B=11,C=12,D=13,E=14,F=15,G=16,H=17,I=18,J=19,K=20,L=21,M=22,
  N=23,O=24,P=25,Q=26,R=27,S=28,T=29,U=30,V=31,W=32,X=33,Y=34,Z=35,
  D0=36,D1=37,D2=38,D3=39,D4=40,D5=41,D6=42,D7=43,D8=44,D9=45,
  ESCAPE=56,CONTROL=57,SHIFT=58,ALT=59,LSYSTEM=60,RSYSTEM=61,
  LBRACKET=63,RBRACKET=64,SEMICOLON=65,COMMA=66,PERIOD=67,
  APOSTROPHE=68,SLASH=69,BACKSLASH=70,TILDE=71,EQUAL=72,
  SPACE=73,RETURN=74,NUMPADENTER=75,BACKSPACE=76,TAB=77,
  PAGEUP=78,PAGEDOWN=79,END=80,HOME=81,INSERT=82,DELETE=83,
  MINUS=84,LEFT=90,RIGHT=91,UP=92,DOWN=93,
  F1=94,F2=95,F3=96,F4=97,F5=98,F6=99,F7=100,F8=101,F9=102,
  F10=103,F11=104,F12=105,
}

local lower = {
  [K.A]='a',[K.B]='b',[K.C]='c',[K.D]='d',[K.E]='e',
  [K.F]='f',[K.G]='g',[K.H]='h',[K.I]='i',[K.J]='j',
  [K.K]='k',[K.L]='l',[K.M]='m',[K.N]='n',[K.O]='o',
  [K.P]='p',[K.Q]='q',[K.R]='r',[K.S]='s',[K.T]='t',
  [K.U]='u',[K.V]='v',[K.W]='w',[K.X]='x',[K.Y]='y',[K.Z]='z',
  [K.D0]='0',[K.D1]='1',[K.D2]='2',[K.D3]='3',[K.D4]='4',
  [K.D5]='5',[K.D6]='6',[K.D7]='7',[K.D8]='8',[K.D9]='9',
  [K.SPACE]=' ',[K.PERIOD]='.',[K.COMMA]=',',[K.SEMICOLON]=';',
  [K.SLASH]='/',[K.BACKSLASH]='\\',[K.APOSTROPHE]="'",
  [K.EQUAL]='=',[K.MINUS]='-',[K.TILDE]='`',
  [K.LBRACKET]='[',[K.RBRACKET]=']',
}
local upper = {
  [K.A]='A',[K.B]='B',[K.C]='C',[K.D]='D',[K.E]='E',
  [K.F]='F',[K.G]='G',[K.H]='H',[K.I]='I',[K.J]='J',
  [K.K]='K',[K.L]='L',[K.M]='M',[K.N]='N',[K.O]='O',
  [K.P]='P',[K.Q]='Q',[K.R]='R',[K.S]='S',[K.T]='T',
  [K.U]='U',[K.V]='V',[K.W]='W',[K.X]='X',[K.Y]='Y',[K.Z]='Z',
  [K.D0]=')',[K.D1]='!',[K.D2]='@',[K.D3]='#',[K.D4]='$',
  [K.D5]='%',[K.D6]='^',[K.D7]='&',[K.D8]='*',[K.D9]='(',
  [K.SPACE]=' ',[K.PERIOD]='>',[K.COMMA]='<',[K.SEMICOLON]=':',
  [K.SLASH]='?',[K.BACKSLASH]='|',[K.APOSTROPHE]='"',
  [K.EQUAL]='+',[K.MINUS]='_',[K.TILDE]='~',
  [K.LBRACKET]='{',[K.RBRACKET]='}',
}

local W, H = term.getSize()
local line = ''
local shifted = false
local mode = 'shell'
local cwd = '/'

local function eval_source(source)
    local exec = computer.exec(source, true)
    if not exec.ok then
        term.writeLine(tostring(exec.error))
    elseif exec.hasResult then
        term.writeLine('=> ' .. tostring(exec.result))
    end
end

local function canonical_path(path)
    if path == nil or path == '' then
        return '/'
    end

    local absolute = path
    if absolute:sub(1, 1) ~= '/' then
        absolute = '/' .. absolute
    end

    local parts = {}
    for part in absolute:gmatch('[^/]+') do
        if part == '.' then
            -- no-op
        elseif part == '..' then
            if #parts > 0 then
                table.remove(parts)
            end
        else
            table.insert(parts, part)
        end
    end

    return '/' .. table.concat(parts, '/')
end

local function join_path(base, rel)
    if rel:sub(1, 1) == '/' then
        return canonical_path(rel)
    end

    if base == '/' then
        return canonical_path('/' .. rel)
    end

    return canonical_path(base .. '/' .. rel)
end

local function contains(tbl, value)
    for i = 1, #tbl do
        if tbl[i] == value then
            return true
        end
    end

    return false
end

local function has_lua_extension(path)
    return path:sub(-4) == '.lua'
end

local function resolve_program_path(path)
    if path == nil or path == '' then
        return nil
    end

    local candidates = {}
    local function add_candidate(candidate)
        if candidate == nil or candidate == '' then
            return
        end

        local canonical = canonical_path(candidate)
        if not contains(candidates, canonical) then
            table.insert(candidates, canonical)
        end
    end

    if path:sub(1, 1) == '/' then
        add_candidate(path)
    elseif path:sub(1, 2) == './' or path:sub(1, 3) == '../' then
        add_candidate(join_path(cwd, path))
    else
        add_candidate(join_path(cwd, path))
        add_candidate('/' .. path)

        if not has_lua_extension(path) then
            add_candidate(join_path(cwd, path .. '.lua'))
            add_candidate('/' .. path .. '.lua')
        end

        if not path:find('/') then
            add_candidate('/bin/' .. path)
            if not has_lua_extension(path) then
                add_candidate('/bin/' .. path .. '.lua')
            end
        end
    end

    for i = 1, #candidates do
        if fs.exists(candidates[i]) then
            return candidates[i]
        end
    end

    return nil
end

local function resolve_directory_path(path)
    if path == nil or path == '' then
        return cwd
    end

    if path:sub(1, 1) == '/' then
        return canonical_path(path)
    end

    return join_path(cwd, path)
end

local function is_directory(path)
    local entries = fs.list(path)
    return not (#entries == 1 and entries[1] == 'Directory not found.')
end

local function run_file(path)
    if path == nil then
        term.writeLine('Program not found.')
        return false
    end

    local source = fs.read(path)
    if source == nil then
        term.writeLine('File not found: ' .. path)
        return false
    end

    local exec = computer.exec(source, false)
    if not exec.ok then
        term.writeLine(tostring(exec.error))
        return false
    end

    if exec.hasResult then
        term.writeLine('=> ' .. tostring(exec.result))
    end

    return true
end

local function load_compat()
    local compat_path = '/lib/cc_compat.lua'
    if not fs.exists(compat_path) then
        return
    end

    local source = fs.read(compat_path)
    if source == nil then
        term.writeLine('Failed to read compat library: ' .. compat_path)
        return
    end

    local exec = computer.exec(source, false)
    if not exec.ok then
        term.writeLine('Compat load error: ' .. tostring(exec.error))
    end
end

local function write_prompt()
    if mode == 'repl' then
        term.write('lua> ')
    else
        term.write('> ')
    end
end

local function draw_shell()
    mode = 'shell'
    line = ''
    term.clear()
    term.setCursorPos(1, 1)
    term.write('Lua Computer v1.0  [' .. W .. 'x' .. H .. ']')
    term.setCursorPos(1, 2)
    term.write('Edit /boot/init.lua to customise this script.')
    term.setCursorPos(1, 4)
    write_prompt()
end

local function draw_repl()
    mode = 'repl'
    line = ''
    term.clear()
    term.setCursorPos(1, 1)
    term.write('Lua REPL')
    term.setCursorPos(1, 2)
    term.write("Type 'exit' to return to shell.")
    term.setCursorPos(1, 4)
    write_prompt()
end

load_compat()
draw_shell()

while true do
    local ev, code, held, ctrl, alt, shift, meta = event.pull()
    if ev == 'key' then
        if code == K.SHIFT then
            shifted = true
        elseif not held then
            if code == K.RETURN or code == K.NUMPADENTER then
                local suppress_prompt = false
                local x, y = term.getCursorPos()
                term.setCursorPos(1, y + 1)

                if mode == 'repl' then
                    if line == 'exit' then
                        draw_shell()
                        suppress_prompt = true
                    elseif line ~= '' then
                        eval_source(line)
                        local x2, y2 = term.getCursorPos()
                        if x2 ~= 1 then
                            term.setCursorPos(1, y2 + 1)
                        end
                    end
                else
                    if line ~= '' then
                        local input = line
                        if input == 'help' then
                            term.writeLine('help          - show this help')
                            term.writeLine('cls           - clear the screen')
                            term.writeLine('cd <path>     - change directory')
                            term.writeLine('pwd           - print current directory')
                            term.writeLine('ls [path]     - list directory')
                            term.writeLine('cat <path>    - print file')
                            term.writeLine('run <path>    - execute Lua file')
                            term.writeLine('termdebug     - run terminal debug demo')
                            term.writeLine('lua           - open Lua REPL terminal')
                            term.writeLine('lua <expr>    - evaluate Lua expression')
                        elseif input == 'cls' or input == 'clear' then
                            line = ''
                            term.clear()
                            term.setCursorPos(1, 1)
                            write_prompt()
                            suppress_prompt = true
                        elseif input == 'pwd' then
                            term.writeLine(cwd)
                        elseif input:sub(1, 3) == 'cd ' then
                            local target = resolve_directory_path(input:sub(4))
                            if is_directory(target) then
                                cwd = target
                            else
                                term.writeLine('Directory not found: ' .. target)
                            end
                        elseif input == 'termdebug' then
                            run_file(resolve_program_path('termdebug.lua'))
                        elseif input:sub(1, 4) == 'run ' then
                            local path = input:sub(5)
                            local resolved = resolve_program_path(path)
                            if resolved == nil then
                                term.writeLine('Program not found: ' .. path)
                            else
                                run_file(resolved)
                            end
                        elseif input == 'lua' then
                            draw_repl()
                            suppress_prompt = true
                        elseif input == 'ls' then
                            local entries = fs.list(cwd)
                            for i = 1, #entries do
                                term.writeLine(entries[i])
                            end
                        elseif input:sub(1, 3) == 'ls ' then
                            local path = resolve_directory_path(input:sub(4))
                            local entries = fs.list(path)
                            for i = 1, #entries do
                                term.writeLine(entries[i])
                            end
                        elseif input:sub(1, 4) == 'cat ' then
                            local raw = input:sub(5)
                            local path
                            if raw:sub(1, 1) == '/' then
                                path = canonical_path(raw)
                            else
                                path = join_path(cwd, raw)
                            end
                            local content = fs.read(path)
                            if content == nil then
                                term.writeLine('File not found: ' .. path)
                            else
                                term.writeLine(content)
                            end
                        else
                            local source = input
                            if input:sub(1, 4) == 'lua ' then
                                source = input:sub(5)
                            end
                            eval_source(source)
                        end

                        local x2, y2 = term.getCursorPos()
                        if not suppress_prompt and x2 ~= 1 then
                            term.setCursorPos(1, y2 + 1)
                        end
                    end
                end

                if not suppress_prompt then
                    line = ''
                    write_prompt()
                end
            elseif code == K.BACKSPACE then
                if #line > 0 then
                    line = line:sub(1, -2)
                    local x, y = term.getCursorPos()
                    local prompt_len = mode == 'repl' and 5 or 2
                    if x > prompt_len + 1 then
                        term.setCursorPos(x - 1, y)
                        term.write(' ')
                        term.setCursorPos(x - 1, y)
                    end
                end
            elseif not ctrl and not alt and not meta then
                local map = shifted and upper or lower
                local ch = map[code]
                if ch then
                    line = line .. ch
                    term.write(ch)
                end
            end
        end
    elseif ev == 'text' then
        if type(code) == 'string' and code ~= '' then
            line = line .. code
            term.write(code)
        end
    elseif ev == 'key_up' then
        if code == K.SHIFT then shifted = false end
    end
end