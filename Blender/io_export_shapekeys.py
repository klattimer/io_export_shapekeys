bl_info = {
    "name": "Export Shape Keys for Unity",
    "author": "Karl Lattimer",
    "version": (0, 0, 1),
    "blender": (2, 7, 4),
    "location": "Export Shape Key for Unity",
    "description": "Exports shape keys and shape key animations for use in Unity",
    "warning": "",
    "url": "https://github.com/klattimer/io_export_shape_keys",
    "category": "Import-Export"}

import bpy
from mathutils import Vector, Color
import time
from bpy.props import *
from bpy_extras.io_utils import ExportHelper, ImportHelper
import os
import json

__version__ = '.'.join(map(str, bl_info["version"]))

# TODO: Add support for IOOrientationHelper to convert the x,y,z

class ExportShapeKeys:
    def __init__(self, exportTool):
        self.exportTool = exportTool
        self.data = {}
        self.data['ShapeKeys'] = {}
        self.data['ShapeKeyAnimations'] = {}

    def collectAnimations(self, ob):
        # Figure out which animations we're exporting if any
        return ob.shape_key_animation_list

    def collectAnimatedShapeKeys(self, ob, startFrame, endFrame):
        shapekeys = []
        s = self.collectShapeKeys(ob)
        for sk in s:
            shapekeys.append(sk)

        initial = []
        changed = []
        bpy.context.scene.frame_set(startFrame)
        for sk in shapekeys:
            initial.append(sk.value)

        for frame in range(startFrame, endFrame):
            bpy.context.scene.frame_set(frame)
            for (i, sk) in enumerate(shapekeys):
                if sk in changed:
                    continue
                if sk.value != initial[i]:
                    changed.append(sk)
        return changed

    def collectShapeKeys(self, ob):
        # Get a list of shape keys on the object
        keys = []
        for shapekey in ob.data.shape_keys.key_blocks:
            if shapekey.name == "Basis": continue
            keys.append(shapekey)
        return keys

    def collectObjects(self):
        # Figure out which objects we want to export shape keys for
        if self.exportTool.selectedonly:
            return bpy.context.selected_objects
        else:
            return bpy.scene.objects

    def validateExportSettings(self):
        pass

    def pre(self, ob):
        """
        Make any backups before we start destroying everything
        """
        print("Prep work started...")

        mesh = ob.data
        vcol = mesh.vertex_colors.active

        global original_materials, mat
        global original_face_mat_indices

        # Deselect all vertices (to avoid odd artefacts, some bug?)
        for n in mesh.vertices:
            n.select = False

        # Store face material indices
        original_face_mat_indices = []
        for n in mesh.polygons:
            original_face_mat_indices.append(n.material_index)
            n.material_index = 0

        # Remember and remove materials
        original_materials = []
        for n in mesh.materials:
            print("Saving Material: " + n.name)
            original_materials.append(n)
        # for n in original_materials:
        temp_i = 0
        for n in mesh.materials:
            mesh.materials.pop(temp_i)
            temp_i = temp_i+1
        # Create new temp material for baking
        mat = bpy.data.materials.new(name="ShapeKeyBake")
        mat.use_vertex_color_paint = True
        mat.diffuse_color = Color([1, 1, 1])
        mat.diffuse_intensity = 1.0
        mat.use_shadeless = True
        mesh.materials.append(mat)
        # mesh.materials[0] = mat

        # Add new vertex color layer for baking
        if len(mesh.vertex_colors) < 8-1:

            vcol = mesh.vertex_colors.new(name="ShapeKeyBake")
            mesh.vertex_colors.active = vcol
            vcol.active_render = True
        else:
            print("Amount limit of vertex color layers exceeded")

    def post(self, ob):
        """
        Put everything back the way it was
        """
        print("Post work started...")
        global original_materials, mat
        global original_face_mat_indices

        mesh = ob.data
        uvtex = mesh.uv_textures.active
        vcol = mesh.vertex_colors.active
        original_face_images = []

        # Remove temp material
        mesh.materials.pop()
        mat.user_clear()
        bpy.data.materials.remove(mat)

        # Restore original materials
        for n in mesh.materials:
            mesh.materials.pop()

        for n in original_materials:
            mesh.materials.append(n)

        # Restore face material indices
        for n in range(len(original_face_mat_indices)-1):
            mesh.polygons[n].material_index = original_face_mat_indices[n]

        # Remove temp vertex color layer
        bpy.ops.mesh.vertex_color_remove()

        # Refresh UI
        bpy.context.scene.frame_current = bpy.context.scene.frame_current

        # Free some memory
        original_materials = []
        original_face_mat_indices = []

    def execute(self):
        self.validateExportSettings()

        # Export all of the shape keys
        objects = self.collectObjects()
        for ob in objects:
            shapeKeys = self.collectShapeKeys(ob)
            self.pre(ob)
            for key in shapeKeys:
                self.generateTexture(ob, key)
            self.post(ob)

            # Export all of the animations
            anims = self.collectAnimations(ob)
            for anim in anims:
                shapeKeys = self.collectAnimatedShapeKeys(ob, anim.startFrame, anim.endFrame)
                if len(shapeKeys) > 0:
                    self.generateAnimationData(shapeKeys, anim)

            # Write the JSON description
            platform = str(bpy.app.build_platform)
            if platform.find("Windows") != -1:
                jsonFilename = self.exportTool.filepath + '\\' + ob.name + '.shapekeys.json'
            else:
                jsonFilename = self.exportTool.filepath + '/' + ob.name + '.shapekeys.json'
            json.dump(self.data, open(jsonFilename, 'w'))

    def generateAnimationData(self, shapeKeys, anim):
        l = []
        for shapeKey in shapeKeys:
            l.append(shapeKey.name)
        if anim.name not in self.data['ShapeKeyAnimations']:
            self.data['ShapeKeyAnimations'][anim.name] = {}
            self.data['ShapeKeyAnimations'][anim.name]['Framerate'] = bpy.context.scene.render.fps
            self.data['ShapeKeyAnimations'][anim.name]['ShapeKeys'] = l
            self.data['ShapeKeyAnimations'][anim.name]['Frames'] = []
        else:
            return
        row = []
        bpy.context.scene.frame_set(anim.startFrame)
        for shapeKey in shapeKeys:
            row.append(shapeKey.value)
        self.data['ShapeKeyAnimations'][anim.name]['StartShape'] = row

        for frame in range(anim.startFrame, anim.endFrame + 1):
            bpy.context.scene.frame_set(frame)
            row = []
            for shapes in shapeKeys:
                row.append(shapes.value)
            self.data['ShapeKeyAnimations'][anim.name]['Frames'].append(row)

    def findLargestDeform(self, ob, key):
        maxDiffX = 0
        maxDiffY = 0
        maxDiffZ = 0

        mesh = ob.data
        print ("Finding largest deform for key " + key.name)
        # Iterate over verts, compare the basis with the shape key and
        # compare the abs value with the existing max
        for n in mesh.vertices:
            diff = n.co.copy() - key.data[n.index].co.copy()
            print("Diff %s\n" % (str(diff[0])+' '+str(diff[1])+' '+str(diff[2])))
            if diff[0] > maxDiffX:
                maxDiffX = diff[0]
            if diff[1] > maxDiffY:
                maxDiffY = diff[1]
            if diff[2] > maxDiffZ:
                maxDiffZ = diff[2]

        # Return a vector of the max diff, this will be exported as "scale"
        # on this shape key
        # FIXME: This must support the selected "direction" UP and FORWARD
        # Right now we're just swapping y and z
        return Vector((maxDiffX, maxDiffZ, maxDiffY))

    def vectorToColor(self, v, scale):
        print ("Encoding: x:%2.2f, y:%2.2f, z:%2.2f" % (v.x, v.y, v.z))

        # Convert vector v to a color of rgb using scale
        # Axis flipping here is annoying, basically we're swapping default
        # blender axis for default unity axis
        if scale.x == 0:
            xscale = 0
        else:
            xscale = 1.0 / scale.x

        if scale.z == 0:
            yscale = 0
        else:
            yscale = 1.0 / scale.z

        if scale.y == 0:
            zscale = 0
        else:
            zscale = 1.0 / scale.y

        out = Vector((((v.x * xscale) + 1.0) / 2.0,
                      ((v.y * yscale) + 1.0) / 2.0,
                      ((v.z * zscale) + 1.0) / 2.0))

        print ("Color will be: r:%2.2f, g:%2.2f, b:%2.2f" % (out.x, out.z, out.y))

        # FIXME: This must support the selected "direction" UP and FORWARD
        # Right now we're just swapping y and z ( x*-1 forward)
        return Color((out.x, out.z, out.y))

    def generateFilename(self, ob, key):
        # Construct complete filepath
        # Adjusted to fix crash in bytes/str issue
        platform = str(bpy.app.build_platform)
        if platform.find("Windows") != -1:
            path = self.exportTool.filepath + '\\' + ob.name + '-' + key.name + '.tga'
        else:
            path = self.exportTool.filepath + '/' + ob.name + '-' + key.name + '.tga'

        return path

    def generateTexture(self, ob, key):
        """
        Generate a shape key texture for object and shape key name
        """
        scale = self.findLargestDeform(ob, key)
        mesh = ob.data
        uvtex = mesh.uv_textures.active
        vcol = mesh.vertex_colors.active

        image = bpy.data.images.new(name="ShapeKeyBake",
                                    width=self.exportTool.width,
                                    height=self.exportTool.height)
        image.generated_width = self.exportTool.width
        image.generated_height = self.exportTool.height
        image.filepath = self.generateFilename(ob, key)

        self.data['ShapeKeys'][key.name] = {}
        self.data['ShapeKeys'][key.name]['Object'] = ob.name
        self.data['ShapeKeys'][key.name]['ImageFile'] = ob.name + '-' + key.name;
        self.data['ShapeKeys'][key.name]['Scale'] = {}
        self.data['ShapeKeys'][key.name]['Scale']['x'] = scale.x
        self.data['ShapeKeys'][key.name]['Scale']['y'] = scale.y
        self.data['ShapeKeys'][key.name]['Scale']['z'] = scale.z
        for n in mesh.vertices:
            diff = n.co.copy() - key.data[n.index].co.copy()

            v = Vector(diff)
            color = self.vectorToColor(v, scale)

            for p in mesh.polygons:
                for i, v in enumerate(p.vertices):
                    if v == n.index:
                        t = p.loop_indices[i]
                        vcol.data[t].color = color

        # assign image to mesh (all uv faces)
        # Simply taking the image from the first face
        original_face_images = []

        for n in uvtex.data:
            if n.image is None:
                original_face_images.append(None)
            else:
                original_face_images.append(n.image.name)

            n.image = image

        # Bake
        render = bpy.context.scene.render

        # Commented out klattimer
        # tempcmv = render.use_color_management;

        render.bake_type = 'TEXTURE'
        render.bake_margin = self.exportTool.margin
        render.use_bake_clear = True
        render.use_bake_selected_to_active = False
        render.bake_quad_split = 'AUTO'
        # render.use_color_management = False

        bpy.ops.object.bake_image()

        image.save()

        # re-assign images to mesh faces
        for n in range(len(original_face_images)):

            tmp = original_face_images[n]
            if original_face_images[n] is not None:
                tmp = bpy.data.images[original_face_images[n]]
            else:
                tmp = None

            uvtex.data[n].image = tmp

        # Remove image from memory
        print(" exported %s" % image.filepath)
        image.user_clear()
        bpy.data.images.remove(image)

        # Removed deprecated code (klattimer)
        # render.use_color_management = tempcmv

        # Tell user what was exported

# ## Blender UI additions
# - List of shape key animation ranges, configurable per object
# - Export menu item


class ShapeKeyAnimationListItem(bpy.types.PropertyGroup):
    """ Group of properties representing an item in the list """

    name = bpy.props.StringProperty(name="Name",
                                    description="Animation Name",
                                    default="Timeline")

    startFrame = bpy.props.IntProperty(name="Start Frame",
                                       description="Start frame of animation range",
                                       default=1,
                                       min=1)
    endFrame = bpy.props.IntProperty(name="End Frame",
                                     description="End frame of animation range",
                                     default=1,
                                     min=1)


class ShapeKeyAnimationListPanel(bpy.types.Panel):
    bl_label = "Shape Key Animation Ranges"
    bl_description = "Define animation timeline ranges for shape keys."
    bl_space_type = "PROPERTIES"
    bl_region_type = "WINDOW"
    bl_context = "data"

    def draw(self, context):
        layout = self.layout
        object = context.object

        row = layout.row()
        row.template_list("ShapeKeyObjectAnimationsList",
                          "The_List",
                          object,
                          "shape_key_animation_list",
                          object,
                          "shape_key_animation_list_index")

        row = layout.row()
        row.operator('animation_list.new_item')
        row.operator('animation_list.delete_item')

        if object.shape_key_animation_list_index >= 0 and len(object.shape_key_animation_list) > 0:
            item = object.shape_key_animation_list[object.shape_key_animation_list_index]

            row = layout.row()
            row.prop(item, "name")
            row.prop(item, "startFrame")
            row.prop(item, "endFrame")


class SKANIM_OT_NewItem(bpy.types.Operator):
    """ Add a new item to the list """

    bl_idname = "animation_list.new_item"
    bl_label = "Add Range"

    def execute(self, context):
        context.object.shape_key_animation_list.add()

        return{'FINISHED'}


class SKANIM_OT_DeleteItem(bpy.types.Operator):
    """ Delete the selected item from the list """

    bl_idname = "animation_list.delete_item"
    bl_label = "Delete Range"

    @classmethod
    def poll(self, context):
        """ Enable if there's something in the list. """

        return len(context.object.shape_key_animation_list) > 0

    def execute(self, context):
        list = context.object.shape_key_animation_list
        index = context.object.shape_key_animation_list_index

        list.remove(index)

        if index > 0:
            index = index - 1

        return{'FINISHED'}


class ShapeKeyObjectAnimationsList(bpy.types.UIList):
    def draw_item(self, context, layout, data, item, icon, active_data, active_propname, index):
        # We could write some code to decide which icon to use here...
        custom_icon = 'OBJECT_DATAMODE'

        # Make sure your code supports all 3 layout types
        if self.layout_type in {'DEFAULT', 'COMPACT'}:
            layout.label(item.name, icon=custom_icon)

        elif self.layout_type in {'GRID'}:
            layout.alignment = 'CENTER'
            layout.label("", icon=custom_icon)


class EXPORT_OT_tools_shapekey_exporter(bpy.types.Operator):
    bl_idname = "object.export_diffmaps_from_shapes"
    bl_description = 'Export shape keys for Unity'
    bl_label = "Export Shape Keys" + " v." + __version__
    bl_space_type = "PROPERTIES"
    bl_region_type = "WINDOW"
    bl_context = "data"

    filename_ext = ".tga"
    filter_glob = StringProperty(default="*.tga", options={'HIDDEN'})

    filepath = StringProperty(name="File Path",
                              description="Filepath used for exporting shape key data files",
                              maxlen=1024,
                              default="",
                              subtype='FILE_PATH')

    # name = StringProperty(name="Name",
    #                      description="Base name for the exported assets",
    #                      maxlen=512,
    #                      default="Name")

    selectedonly = BoolProperty(name="Selected Only",
                                description="Export only selected objects",
                                default=True)

    shapeson = BoolProperty(name="Export Shape Keys",
                            description="Save shapekeys as TGA images",
                            default=True)

    width = IntProperty(name="Width",
                        description="Width of shape key image to export",
                        default=512,
                        min=64,
                        max=8192)

    height = IntProperty(name="Height",
                         description="Height of shape key image to export",
                         default=512,
                         min=64,
                         max=8192)

    animationson = BoolProperty(name="Export Animations",
                                description="Save shape key animations",
                                default=True)

    margin = IntProperty(name="Edge Margin",
                         description="Sets outside margin around UV edges",
                         default=10,
                         min=0,
                         max=64)

    def draw(self, context):
        layout = self.layout

        filepath = os.path.dirname(self.filepath)

        os.path.join(filepath)

        row = layout.row()
        # row.prop(self, "name")

        col = layout.column(align=True)
        col.prop(self, "width")
        col.prop(self, "height")
        col.prop(self, "margin")

        col = layout.column(align=False)

        # Removed, causing crash (klattimer)
        # me = context.active_object.data
        # col.template_list(me, "uv_textures", me.uv_textures, "active_index", rows=2)

        col = layout.column(align=False)
        col.prop(self, "shapeson")
        col.prop(self, "animationson")
        col.prop(self, "selectedonly")

        # row = layout.row()
        # row.prop(self, "axis_forward")

        # row = layout.row()
        # row.prop(self, "axis_up")

    def execute(self, context):
        # name = context.active_object.name

        start = time.time()

        # -- Perform the export operation

        exporter = ExportShapeKeys(self)
        exporter.execute()


        # -- End
        print ("Time elapsed:", time.time() - start, "seconds.")

        return {'FINISHED'}

    def invoke(self, context, event):
        wm = context.window_manager
        wm.fileselect_add(self)
        return {'RUNNING_MODAL'}


def menu_func(self, context):
    if bpy.data.filepath:
        default_path = os.path.split(bpy.data.filepath)[0] + "/"
        self.layout.operator(EXPORT_OT_tools_shapekey_exporter.bl_idname, text="Export Shape Key Animation for Unity").filepath = default_path
    else:
        self.layout.operator(EXPORT_OT_tools_shapekey_exporter.bl_idname, text="Export Shape Key Animation for Unity")


def register():
    bpy.utils.register_module(__name__)
    bpy.types.INFO_MT_file_export.append(menu_func)

    bpy.types.Object.shape_key_animation_list = bpy.props.CollectionProperty(type=ShapeKeyAnimationListItem)
    bpy.types.Object.shape_key_animation_list_index = bpy.props.IntProperty(name="Index for animation_list", default=0)


def unregister():
    bpy.utils.unregister_module(__name__)
    bpy.types.INFO_MT_file_export.remove(menu_func)

    del bpy.types.Object.shape_key_animation_list
    del bpy.types.Object.shape_key_animation_list_index

if __name__ == "__main__":
    register()
