-- Filesystem/path API for programmable computers.
-- Exposes:
--   canonical_path(path)
--   join_path(base, rel)
--   resolve_program_path(path, cwd)
--   resolve_directory_path(path, cwd)
--   is_directory(path)

function canonical_path(path)
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

function join_path(base, rel)
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

function resolve_program_path(path, cwd)
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

function resolve_directory_path(path, cwd)
    if path == nil or path == '' then
        return cwd
    end

    if path:sub(1, 1) == '/' then
        return canonical_path(path)
    end

    return join_path(cwd, path)
end

function is_directory(path)
    local entries = fs.list(path)
    return not (#entries == 1 and entries[1] == 'Directory not found.')
end
