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

-- layout: 0=us, 1=qwertz, 2=azerty  (packed into key event by client)
local function map_letter(code, shift, layout)
    local letter = nil

    if layout == 1 then -- qwertz
        if code == K.Y then
            letter = 'z'
        elseif code == K.Z then
            letter = 'y'
        end
    elseif layout == 2 then -- azerty
        if code == K.Q then
            letter = 'a'
        elseif code == K.A then
            letter = 'q'
        elseif code == K.W then
            letter = 'z'
        elseif code == K.Z then
            letter = 'w'
        elseif code == K.SEMICOLON then
            letter = 'm'
        end
    end

    if letter == nil and code >= K.A and code <= K.Z then
        letter = string.char(string.byte('a') + (code - K.A))
    end

    if letter == nil then
        return nil
    end

    return shift and string.upper(letter) or letter
end

local function map_symbol(code, shift, layout)
    if layout == 1 then -- qwertz
        local normal = {
            [K.D0] = '0', [K.D1] = '1', [K.D2] = '2', [K.D3] = '3', [K.D4] = '4',
            [K.D5] = '5', [K.D6] = '6', [K.D7] = '7', [K.D8] = '8', [K.D9] = '9',
            [K.TILDE] = '^', [K.RBRACKET] = '+', [K.BACKSLASH] = '#',
            [K.COMMA] = ',', [K.PERIOD] = '.', [K.SLASH] = '-', [K.SPACE] = ' '
        }
        local shifted = {
            [K.D0] = '=', [K.D1] = '!', [K.D2] = '"', [K.D4] = '$', [K.D5] = '%',
            [K.D6] = '&', [K.D7] = '/', [K.D8] = '(', [K.D9] = ')',
            [K.MINUS] = '?', [K.EQUAL] = '`', [K.LBRACKET] = '*', [K.RBRACKET] = "'",
            [K.COMMA] = ';', [K.PERIOD] = ':', [K.SLASH] = '_'
        }
        return shift and shifted[code] or normal[code]
    elseif layout == 2 then -- azerty
        local normal = {
            [K.D1] = '&', [K.D3] = '"', [K.D4] = "'", [K.D5] = '(', [K.D6] = '-',
            [K.D8] = '_', [K.MINUS] = ')', [K.EQUAL] = '=', [K.LBRACKET] = '$',
            [K.RBRACKET] = '*', [K.COMMA] = ';', [K.PERIOD] = ':', [K.SLASH] = '!',
            [K.SPACE] = ' '
        }
        local shifted = {
            [K.D0] = '0', [K.D1] = '1', [K.D2] = '2', [K.D3] = '3', [K.D4] = '4',
            [K.D5] = '5', [K.D6] = '6', [K.D7] = '7', [K.D8] = '8', [K.D9] = '9',
            [K.EQUAL] = '+', [K.LBRACKET] = '^', [K.APOSTROPHE] = '%',
            [K.COMMA] = '?', [K.PERIOD] = '.', [K.SLASH] = '/'
        }
        return shift and shifted[code] or normal[code]
    else -- us (layout == 0)
        local normal = {
            [K.D0] = '0', [K.D1] = '1', [K.D2] = '2', [K.D3] = '3', [K.D4] = '4',
            [K.D5] = '5', [K.D6] = '6', [K.D7] = '7', [K.D8] = '8', [K.D9] = '9',
            [K.TILDE] = '`', [K.MINUS] = '-', [K.EQUAL] = '=', [K.LBRACKET] = '[',
            [K.RBRACKET] = ']', [K.BACKSLASH] = '\\', [K.SEMICOLON] = ';',
            [K.APOSTROPHE] = "'", [K.COMMA] = ',', [K.PERIOD] = '.', [K.SLASH] = '/',
            [K.SPACE] = ' '
        }
        local shifted = {
            [K.D0] = ')', [K.D1] = '!', [K.D2] = '@', [K.D3] = '#', [K.D4] = '$',
            [K.D5] = '%', [K.D6] = '^', [K.D7] = '&', [K.D8] = '*', [K.D9] = '(',
            [K.TILDE] = '~', [K.MINUS] = '_', [K.EQUAL] = '+', [K.LBRACKET] = '{',
            [K.RBRACKET] = '}', [K.BACKSLASH] = '|', [K.SEMICOLON] = ':',
            [K.APOSTROPHE] = '"', [K.COMMA] = '<', [K.PERIOD] = '>', [K.SLASH] = '?'
        }
        return shift and shifted[code] or normal[code]
    end
end

local function keycode_to_text(code, shift, layout)
    local letter = map_letter(code, shift, layout)
    if letter ~= nil then
        return letter
    end

    return map_symbol(code, shift, layout)
end

local W, H = term.getSize()
local line = ''
local cursor = 1
local mode = 'shell'
local cwd = '/'
local input_row = 1
local shell_history = {}
local repl_history = {}
local history_index = nil
local history_stash = ''

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
    local _, y = term.getCursorPos()
    input_row = y
    if mode == 'repl' then
        term.write('lua> ')
    else
        term.write('> ')
    end
end

local function prompt_text()
    return mode == 'repl' and 'lua> ' or '> '
end

local function active_history()
    return mode == 'repl' and repl_history or shell_history
end

local function reset_history_navigation()
    history_index = nil
    history_stash = ''
end

local function add_history_entry(entry)
    if entry == nil or entry == '' then
        return
    end

    local history = active_history()
    if history[#history] ~= entry then
        table.insert(history, entry)
    end

    if #history > 100 then
        table.remove(history, 1)
    end
end

local function render_input_line()
    local prompt = prompt_text()
    term.setCursorPos(1, input_row)
    term.clearLine()
    term.write(prompt .. line)
    term.setCursorPos(#prompt + cursor, input_row)
end

local function set_line(new_line)
    line = new_line or ''
    cursor = #line + 1
    render_input_line()
end

local function navigate_history(delta)
    local history = active_history()
    if #history == 0 then
        return
    end

    if history_index == nil then
        if delta < 0 then
            history_stash = line
            history_index = #history
        else
            return
        end
    else
        history_index = history_index + delta
        if history_index < 1 then
            history_index = 1
        elseif history_index > #history then
            history_index = nil
            set_line(history_stash)
            history_stash = ''
            return
        end
    end

    set_line(history[history_index] or '')
end

local function draw_shell()
    mode = 'shell'
    line = ''
    cursor = 1
    reset_history_navigation()
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
    cursor = 1
    reset_history_navigation()
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
    local ev, code, held, ctrl, alt, shift, meta, layout = event.pull()
    if ev == 'key' then
            layout = layout or 0

            if (code == K.RETURN or code == K.NUMPADENTER) and not held then
                local suppress_prompt = false
                local input = line
                local x, y = term.getCursorPos()
                term.setCursorPos(1, y + 1)

                if mode == 'repl' then
                    if input == 'exit' then
                        add_history_entry(input)
                        draw_shell()
                        suppress_prompt = true
                    elseif input ~= '' then
                        add_history_entry(input)
                        eval_source(input)
                        local x2, y2 = term.getCursorPos()
                        if x2 ~= 1 then
                            term.setCursorPos(1, y2 + 1)
                        end
                    end
                else
                    if input ~= '' then
                        add_history_entry(input)
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
                            cursor = 1
                            reset_history_navigation()
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
                    cursor = 1
                    reset_history_navigation()
                    write_prompt()
                end
            elseif code == K.BACKSPACE then
                if cursor > 1 and #line > 0 then
                    line = line:sub(1, cursor - 2) .. line:sub(cursor)
                    cursor = cursor - 1
                    reset_history_navigation()
                    render_input_line()
                end
            elseif code == K.LEFT then
                if cursor > 1 then
                    cursor = cursor - 1
                    render_input_line()
                end
            elseif code == K.RIGHT then
                if cursor <= #line then
                    cursor = cursor + 1
                    render_input_line()
                end
            elseif code == K.HOME then
                cursor = 1
                render_input_line()
            elseif code == K.END then
                cursor = #line + 1
                render_input_line()
            elseif code == K.DELETE then
                if cursor <= #line then
                    line = line:sub(1, cursor - 1) .. line:sub(cursor + 1)
                    reset_history_navigation()
                    render_input_line()
                end
            elseif code == K.UP then
                navigate_history(-1)
            elseif code == K.DOWN then
                navigate_history(1)
            elseif not ctrl and not alt and not meta then
                local typed = keycode_to_text(code, shift, layout)
                if typed ~= nil and typed ~= '' then
                    line = line:sub(1, cursor - 1) .. typed .. line:sub(cursor)
                    cursor = cursor + #typed
                    reset_history_navigation()
                    render_input_line()
                end
            end
        end
end