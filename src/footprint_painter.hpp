#pragma once

#include <cstdint>

#include <godot_cpp/classes/node.hpp>
#include <godot_cpp/classes/resource_loader.hpp>
#include <godot_cpp/classes/texture2drd.hpp>
#include <godot_cpp/classes/rendering_device.hpp>

#include <godot_cpp/classes/rd_texture_format.hpp>
#include <godot_cpp/classes/rd_texture_view.hpp>
#include <godot_cpp/classes/rd_sampler_state.hpp>
#include <godot_cpp/classes/rd_shader_file.hpp>
#include <godot_cpp/classes/rd_shader_spirv.hpp>
#include <godot_cpp/classes/rd_uniform.hpp>

#include <godot_cpp/classes/rendering_server.hpp>

namespace godot
{

class FootprintPainter: public Node
{
    GDCLASS(FootprintPainter, Node);

    struct TPParams {
        Vector4 rotation_mat;
        Vector2 center_left;
        Vector2 center_right;
        float depth_left;
        float depth_right;
        int32_t texture_size;
        float downscale_factor;
    };

    public:
        static constexpr std::string_view TP_SHADER_PATH = "res://shaders/disp_compute.glsl";
        Ref<Texture2DRD> displacement_texture;

    private:
        // Exported
        Ref<Texture2D> footprint_texture;
        int32_t texture_size = 256; 
        TPParams params;
        RenderingDevice *device;
        RID shader;
        RID pipeline;
        RID compute_texture;
        RID footprint_texture_rid;
        RID footprint_sampler;
        RID uniform_set;
        TypedArray<Ref<RDUniform>> uniforms;
        Ref<RDTextureFormat> format;
        Ref<RDTextureView> view;

    public:
        void _ready() override;
        void _process(double delta) override;
        void _exit_tree() override;

        void update_feet(Vector2 p_pos_left, Vector2 p_pos_right, float p_depth_left, float p_depth_right);
        void set_angle(float p_angle);

    protected:
        static void _bind_methods();

    private:
        void init_compute();
        void init_shader();
        void init_target_texture();
        void init_footprint_texture();
        void init_uniforms();
        void dispatch_compute();

        void set_texture_size(const int32_t p_size);
        void set_footprint_texture(const Ref<Texture2D>& p_texture);
        int32_t get_texture_size();
        Ref<Texture2D> get_footprint_texture();

        inline void free_if_valid(const RID& p_rid, const char* debug_msg)
        {
            WARN_PRINT(debug_msg);
            if (p_rid.is_valid()) device->free_rid(p_rid);
        }

    public:
        FootprintPainter();
        ~FootprintPainter();
};

} // namespace godot
