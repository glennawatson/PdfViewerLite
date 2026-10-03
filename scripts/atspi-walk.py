#!/usr/bin/python3
# Copyright (c) 2026 Glenn Watson. All rights reserved.
# Glenn Watson licenses this file to you under the MIT license.
# See the LICENSE file in the project root for full license information.
"""Walks an application's AT-SPI tree the way a screen reader does and checks every control has a spoken name.

Usage: atspi-walk.py <a11y bus address> <application bus name> <minimum controls>
Exits non-zero when the window is missing, too few controls are exposed, or a control's name is empty or a type name.
"""
import sys

from gi.repository import Gio, GLib

CONTROLS = {'push button', 'toggle button', 'combo box', 'entry', 'text', 'check box', 'radio button', 'slider',
            'page tab', 'menu item'}

address, name, minimum = sys.argv[1], sys.argv[2], int(sys.argv[3])
flags = Gio.DBusConnectionFlags.AUTHENTICATION_CLIENT | Gio.DBusConnectionFlags.MESSAGE_BUS_CONNECTION
bus = Gio.DBusConnection.new_for_address_sync(address, flags, None, None)


def call(path, interface, method, args=None):
    return bus.call_sync(name, path, interface, method, args, None, Gio.DBusCallFlags.NONE, 3000, None).unpack()


def accessible_name(path):
    args = GLib.Variant('(ss)', ('org.a11y.atspi.Accessible', 'Name'))
    return call(path, 'org.freedesktop.DBus.Properties', 'Get', args)[0]


frames, named, bad, seen = [], [], [], set()


def walk(path):
    if path in seen:
        return
    seen.add(path)
    role = call(path, 'org.a11y.atspi.Accessible', 'GetRoleName')[0]
    spoken = accessible_name(path).strip()
    if role == 'frame':
        frames.append(spoken)
    elif role in CONTROLS:
        (bad if not spoken or spoken.startswith('Avalonia.') else named).append(f'{role}: {spoken!r}')
    for _, child in call(path, 'org.a11y.atspi.Accessible', 'GetChildren')[0]:
        walk(child)


walk('/org/a11y/atspi/accessible/root')
print('windows:', frames)
print(f'{len(named)} named controls')
for line in bad:
    print('needs a name ->', line)
if not frames or len(named) < minimum or bad:
    sys.exit(1)
