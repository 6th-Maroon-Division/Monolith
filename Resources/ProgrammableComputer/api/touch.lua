-- Touch input API for programmable computers.
-- Exposes:
--   touch_on(event_name, handler)
--   touch_off(event_name, handler)
--   touch_emit(event_name, ...)
--   touch_on_cell(x, y, handler)
--   touch_off_cell(binding)

local _touch_listeners = {
    touch = {},
    touch_pressed = {},
}

local function _touch_listener_bucket(event_name)
    local bucket = _touch_listeners[event_name]
    if bucket == nil then
        bucket = {}
        _touch_listeners[event_name] = bucket
    end

    return bucket
end

function touch_on(event_name, handler)
    if type(event_name) ~= 'string' or type(handler) ~= 'function' then
        return false
    end

    local bucket = _touch_listener_bucket(event_name)
    bucket[#bucket + 1] = handler
    return true
end

function touch_off(event_name, handler)
    if type(event_name) ~= 'string' or type(handler) ~= 'function' then
        return false
    end

    local bucket = _touch_listeners[event_name]
    if bucket == nil then
        return false
    end

    for i = #bucket, 1, -1 do
        if bucket[i] == handler then
            table.remove(bucket, i)
            return true
        end
    end

    return false
end

function touch_emit(event_name, ...)
    local bucket = _touch_listeners[event_name]
    if bucket == nil or #bucket == 0 then
        return
    end

    for i = 1, #bucket do
        if type(bucket[i]) == 'function' then
            if type(pcall) == 'function' then
                local ok, err = pcall(bucket[i], ...)
                if not ok then
                    term.writeLine('touch listener error [' .. tostring(event_name) .. ']: ' .. tostring(err))
                end
            else
                bucket[i](...)
            end
        end
    end
end

-- Binds one callback for a specific cell coordinate.
function touch_on_cell(x, y, handler)
    if type(x) ~= 'number' or type(y) ~= 'number' or type(handler) ~= 'function' then
        return nil
    end

    local binding = {
        x = x,
        y = y,
    }

    binding.on_touch = function(cell_x, cell_y, button)
        if cell_x == binding.x and cell_y == binding.y then
            handler(cell_x, cell_y, button)
        end
    end

    touch_on('touch_pressed', binding.on_touch)
    return binding
end

function touch_off_cell(binding)
    if type(binding) ~= 'table' or type(binding.on_touch) ~= 'function' then
        return false
    end

    return touch_off('touch_pressed', binding.on_touch)
end
